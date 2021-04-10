using JpegLibrary.ScanDecoder;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JpegLibrary
{
    /// <summary>
    /// The decoder to decode image from JPEG stream.
    /// </summary>
    public class JpegDecoder<TWriter> : IJpegDecoder<TWriter> where TWriter : notnull, IJpegBlockOutputWriter
    {
        private ReadOnlySequence<byte> _inputBuffer;

        private JpegFrameHeader? _frameHeader;
        private ushort _restartInterval;
        private byte? _maxHorizontalSamplingFactor;
        private byte? _maxVerticalSamplingFactor;

        private TWriter? _outputWriter;
        private JpegScanDecoder? _scanDecoder;

        private List<JpegQuantizationTable>? _quantizationTables;
        private List<JpegHuffmanDecodingTable>? _huffmanTables;
        private List<JpegArithmeticDecodingTable>? _arithmeticTables;

        /// <summary>
        /// Get or set the memory pool to use when allocating large chunks of temporary buffer.
        /// </summary>
        public MemoryPool<byte>? MemoryPool { get; set; }

        /// <summary>
        /// Get or set the StartOfFrame marker of this image.
        /// </summary>
        public JpegMarker StartOfFrame { get; set; }

        /// <summary>
        /// Set JPEG stream content to decode.
        /// </summary>
        /// <param name="input">The JPEG stream.</param>
        public void SetInput(ReadOnlyMemory<byte> input)
            => SetInput(new ReadOnlySequence<byte>(input));

        /// <summary>
        /// Set JPEG stream content to decode.
        /// </summary>
        /// <param name="input">The JPEG stream.</param>
        public void SetInput(ReadOnlySequence<byte> input)
        {
            _inputBuffer = input;

            _frameHeader = null;
            _restartInterval = 0;
        }


        /// <summary>
        /// Scan the stream for JPEG image information.
        /// </summary>
        /// <returns>The length of the JPEG stream.</returns>
        public int Identify() => Identify(false);

        /// <summary>
        /// Scan the stream for JPEG image information.
        /// </summary>
        /// <param name="loadQuantizationTables">True to load quantization tables. This is required if you want to estimate the quality factor of the image.</param>
        /// <returns>The length of the JPEG stream.</returns>
        public virtual int Identify(bool loadQuantizationTables)
        {
            JpegReader reader = new JpegReader(_inputBuffer);

            // Reset frame header
            _frameHeader = default;

            bool toContinue = true;
            while (toContinue && !reader.IsEmpty)
            {
                // Read next marker
                if (!reader.TryReadMarker(out JpegMarker marker))
                {
                    ThrowInvalidDataException(reader.ConsumedByteCount, "No marker found.");
                }

                toContinue = ProcessMarkerForIdentification(marker, ref reader, loadQuantizationTables);
            }

            if (_frameHeader is null)
            {
                throw new InvalidOperationException("Frame header was not found.");
            }

            return reader.ConsumedByteCount;
        }


        /// <summary>
        /// This routine is called when <see cref="Identify(bool)"/> meets a marker in the JPEG stream.
        /// </summary>
        /// <param name="marker">The JPEG marker.</param>
        /// <param name="reader">The JPEG reader.</param>
        /// <param name="loadQuantizationTables">True if quantization tables should be loaded.</param>
        /// <returns>True if <see cref="Identify(bool)"/> should continue reading.</returns>
        protected virtual bool ProcessMarkerForIdentification(JpegMarker marker, ref JpegReader reader, bool loadQuantizationTables)
        {
            switch (marker)
            {
                case JpegMarker.StartOfImage:
                    break;
                case JpegMarker.StartOfFrame0:
                case JpegMarker.StartOfFrame1:
                case JpegMarker.StartOfFrame2:
                case JpegMarker.StartOfFrame3:
                case JpegMarker.StartOfFrame9:
                case JpegMarker.StartOfFrame10:
                case JpegMarker.StartOfFrame5:
                case JpegMarker.StartOfFrame6:
                case JpegMarker.StartOfFrame7:
                case JpegMarker.StartOfFrame11:
                case JpegMarker.StartOfFrame13:
                case JpegMarker.StartOfFrame14:
                case JpegMarker.StartOfFrame15:
                    StartOfFrame = marker;
                    ProcessFrameHeader(ref reader, false, false);
                    break;
                case JpegMarker.StartOfScan:
                    ProcessScanHeader(ref reader, true);
                    break;
                case JpegMarker.DefineRestartInterval:
                    ProcessDefineRestartInterval(ref reader);
                    break;
                case JpegMarker.DefineQuantizationTable:
                    ProcessDefineQuantizationTable(ref reader, loadQuantizationTables);
                    break;
                case JpegMarker.DefineRestart0:
                case JpegMarker.DefineRestart1:
                case JpegMarker.DefineRestart2:
                case JpegMarker.DefineRestart3:
                case JpegMarker.DefineRestart4:
                case JpegMarker.DefineRestart5:
                case JpegMarker.DefineRestart6:
                case JpegMarker.DefineRestart7:
                    break;
                case JpegMarker.EndOfImage:
                    return false;
                default:
                    ProcessOtherMarker(ref reader);
                    break;
            }

            return true;
        }


        /// <summary>
        /// Estimate the image quality factor from quantization tables.
        /// </summary>
        /// <param name="quality">The estimated quality factor.</param>
        /// <returns>True if the quality is estimated. False when quantization tables don't exists or loaded.</returns>
        public bool TryEstimateQuanlity(out float quality)
        {
            if (_quantizationTables is null)
            {
                quality = 0;
                return false;
            }

            // Luminance
            JpegQuantizationTable quantizationTable = GetQuantizationTable(0);
            if (quantizationTable.IsEmpty)
            {
                quality = 0;
                return false;
            }
            quality = EstimateQuality(quantizationTable, JpegStandardQuantizationTable.GetLuminanceTable(0, 0), out _);

            // Chrominance
            quantizationTable = GetQuantizationTable(1);
            if (!quantizationTable.IsEmpty)
            {
                float quality2 = EstimateQuality(quantizationTable, JpegStandardQuantizationTable.GetChrominanceTable(0, 0), out _);
                quality = Math.Min(quality, quality2);
            }

            quality = JpegMathHelper.Clamp(quality, 0f, 100f);
            return true;
        }

        private static float EstimateQuality(JpegQuantizationTable quantizationTable, JpegQuantizationTable standardTable, out float dVariance)
        {
            Debug.Assert(!quantizationTable.IsEmpty);
            Debug.Assert(!standardTable.IsEmpty);

            bool allOnes = true;
            double dSumPercent = 0;
            double dSumPercentSqr = 0;

            ref ushort elementRef = ref MemoryMarshal.GetReference(quantizationTable.Elements);
            ref ushort standardRef = ref MemoryMarshal.GetReference(standardTable.Elements);

            for (int i = 0; i < 64; i++)
            {
                double dComparePercent;

                ushort element = Unsafe.Add(ref elementRef, i);
                if (element == 0)
                {
                    dComparePercent = 999.99;
                }
                else
                {
                    ushort standard = Unsafe.Add(ref standardRef, i);
                    dComparePercent = 100.0 * element / standard;
                }

                dSumPercent += dComparePercent;
                dSumPercentSqr += dComparePercent * dComparePercent;

                if (element != 1)
                {
                    allOnes = false;
                }
            }

            // Perform some statistical analysis of the quality factor
            // to determine the likelihood of the current quantization
            // table being a scaled version of the "standard" tables.
            // If the variance is high, it is unlikely to be the case.
            dSumPercent /= 64.0;    /* mean scale factor */
            dSumPercentSqr /= 64.0;
            dVariance = (float)(dSumPercentSqr - (dSumPercent * dSumPercent)); /* variance */

            // Generate the equivalent IJQ "quality" factor
            if (allOnes)      /* special case for all-ones table */
                return 100.0f;
            else if (dSumPercent <= 100.0)
                return (float)((200.0 - dSumPercent) / 2.0);
            else
                return (float)(5000.0 / dSumPercent);
        }

        private static void ProcessOtherMarker(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryAdvance(length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data reached.");
                return;
            }
        }


        private void ProcessFrameHeader(ref JpegReader reader, bool metadataOnly, bool overrideAllowed)
        {
            // Read length field
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
                return;
            }
            if (!JpegFrameHeader.TryParse(buffer, metadataOnly, out JpegFrameHeader frameHeader, out int bytesConsumed))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount - length + bytesConsumed, "Failed to parse frame header.");
                return;
            }
            if (!overrideAllowed && _frameHeader.HasValue)
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Multiple frame is not supported.");
                return;
            }
            _frameHeader = frameHeader;
        }


        private static JpegScanHeader ProcessScanHeader(ref JpegReader reader, bool metadataOnly)
        {

            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
            }
            if (!JpegScanHeader.TryParse(buffer, metadataOnly, out JpegScanHeader scanHeader, out int bytesConsumed))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount - length + bytesConsumed, "Failed to parse scan header.");
            }
            return scanHeader;
        }

        /// <summary>
        /// Load JPEG tables from the specified buffer.
        /// </summary>
        /// <param name="content">The byte buffer that contains JPEG table definitions.</param>
        public void LoadTables(Memory<byte> content) => LoadTables(new ReadOnlySequence<byte>(content));

        /// <summary>
        /// Load JPEG tables from the specified buffer.
        /// </summary>
        /// <param name="content">The byte buffer that contains JPEG table definitions.</param>
        public void LoadTables(ReadOnlySequence<byte> content)
        {
            JpegReader reader = new JpegReader(content);

            while (!reader.IsEmpty)
            {
                // Read next marker
                if (!reader.TryReadMarker(out JpegMarker marker))
                {
                    return;
                }

                switch (marker)
                {
                    case JpegMarker.StartOfImage:
                        break;
                    case JpegMarker.DefineRestart0:
                    case JpegMarker.DefineRestart1:
                    case JpegMarker.DefineRestart2:
                    case JpegMarker.DefineRestart3:
                    case JpegMarker.DefineRestart4:
                    case JpegMarker.DefineRestart5:
                    case JpegMarker.DefineRestart6:
                    case JpegMarker.DefineRestart7:
                        break;
                    case JpegMarker.DefineHuffmanTable:
                        ProcessDefineHuffmanTable(ref reader);
                        break;
                    case JpegMarker.DefineArithmeticCodingConditioning:
                        ProcessDefineArithmeticCodingConditioning(ref reader);
                        break;
                    case JpegMarker.DefineQuantizationTable:
                        ProcessDefineQuantizationTable(ref reader, loadQuantizationTables: true);
                        break;
                    case JpegMarker.DefineRestartInterval:
                        ProcessDefineRestartInterval(ref reader);
                        break;
                    case JpegMarker.EndOfImage:
                        return;
                    default:
                        ProcessOtherMarker(ref reader);
                        break;
                }
            }
        }

        [DoesNotReturn]
        private static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }

        [DoesNotReturn]
        private static void ThrowInvalidDataException(int offset, string message)
        {
            throw new InvalidDataException($"Failed to decode JPEG data at offset {offset}. {message}");
        }


        private JpegFrameHeader GetFrameHeader() => _frameHeader.HasValue ? _frameHeader.GetValueOrDefault() : throw new InvalidOperationException("Call Identify() before this operation.");

        /// <summary>
        /// Get the image width from the frame header.
        /// </summary>
        public int Width => GetFrameHeader().SamplesPerLine;

        /// <summary>
        /// Get the image height from the frame header.
        /// </summary>
        public int Height => GetFrameHeader().NumberOfLines;

        /// <summary>
        /// Get the image precision from the frame header.
        /// </summary>
        public int Precision => GetFrameHeader().SamplePrecision;

        /// <summary>
        /// Get the number of components from the frame header.
        /// </summary>
        public int NumberOfComponents => GetFrameHeader().NumberOfComponents;

        /// <summary>
        /// Set the frame header.
        /// </summary>
        /// <param name="frameHeader">The JPEG frame header.</param>
        public void SetFrameHeader(JpegFrameHeader frameHeader)
        {
            _frameHeader = frameHeader;
        }

        /// <summary>
        /// Get the maximum horizontal sampling factor.
        /// </summary>
        /// <returns>The maximum horizontal sampling factor</returns>
        public int GetMaximumHorizontalSampling()
        {
            if (_maxHorizontalSamplingFactor.HasValue)
            {
                return _maxHorizontalSamplingFactor.GetValueOrDefault();
            }
            JpegFrameHeader frameHeader = GetFrameHeader();
            if (frameHeader.Components is null)
            {
                throw new InvalidOperationException();
            }
            int maxHorizontalSampling = 1;
            foreach (JpegFrameComponentSpecificationParameters currentFrameComponent in frameHeader.Components)
            {
                maxHorizontalSampling = Math.Max(maxHorizontalSampling, currentFrameComponent.HorizontalSamplingFactor);
            }
            _maxHorizontalSamplingFactor = (byte)maxHorizontalSampling;
            return maxHorizontalSampling;
        }

        /// <summary>
        /// Get the maximum vertical sampling factor.
        /// </summary>
        /// <returns>The maximum vertical sampling factor</returns>
        public int GetMaximumVerticalSampling()
        {
            if (_maxVerticalSamplingFactor.HasValue)
            {
                return _maxVerticalSamplingFactor.GetValueOrDefault();
            }
            JpegFrameHeader frameHeader = GetFrameHeader();
            if (frameHeader.Components is null)
            {
                throw new InvalidOperationException();
            }
            int maxVerticalSampling = 1;
            foreach (JpegFrameComponentSpecificationParameters currentFrameComponent in frameHeader.Components)
            {
                maxVerticalSampling = Math.Max(maxVerticalSampling, currentFrameComponent.VerticalSamplingFactor);
            }
            _maxVerticalSamplingFactor = (byte)maxVerticalSampling;
            return maxVerticalSampling;
        }

        /// <summary>
        /// Get the horizontal sample for the specified component.
        /// </summary>
        /// <param name="componentIndex">The index of the component.</param>
        /// <returns>The horizontal sample of the component.</returns>
        public byte GetHorizontalSampling(int componentIndex)
        {
            JpegFrameHeader frameHeader = GetFrameHeader();
            JpegFrameComponentSpecificationParameters[]? components = frameHeader.Components;
            if (components is null)
            {
                throw new InvalidOperationException();
            }
            if ((uint)componentIndex >= (uint)components.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(componentIndex));
            }
            return components[componentIndex].HorizontalSamplingFactor;
        }

        /// <summary>
        /// Get the vertical sample for the specified component.
        /// </summary>
        /// <param name="componentIndex">The index of the component.</param>
        /// <returns>The vertical sample of the component.</returns>
        public byte GetVerticalSampling(int componentIndex)
        {
            JpegFrameHeader frameHeader = GetFrameHeader();
            JpegFrameComponentSpecificationParameters[]? components = frameHeader.Components;
            if (components is null)
            {
                throw new InvalidOperationException();
            }
            if ((uint)componentIndex >= (uint)components.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(componentIndex));
            }
            return components[componentIndex].VerticalSamplingFactor;
        }

        /// <summary>
        /// Set the output buffer writer.
        /// </summary>
        /// <param name="outputWriter">The output buffer writer.</param>
        public void SetOutputWriter(TWriter outputWriter)
        {
            _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        }

        /// <summary>
        /// Decode the image from the JPEG stream.
        /// </summary>
        public void Decode()
        {
            if (_inputBuffer.IsEmpty)
            {
                throw new InvalidOperationException("Input buffer is not specified.");
            }
            if (_outputWriter is null)
            {
                throw new InvalidOperationException("The output buffer is not specified.");
            }

            JpegReader reader = new JpegReader(_inputBuffer);
            _scanDecoder = null;

            // SOI marker
            if (!reader.TryReadStartOfImageMarker())
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Marker StartOfImage not found.");
                return;
            }

            try
            {
                bool toContinue = true;
                while (toContinue && !reader.IsEmpty)
                {
                    // Read next marker
                    if (!reader.TryReadMarker(out JpegMarker marker))
                    {
                        ThrowInvalidDataException(reader.ConsumedByteCount, "No marker found.");
                        return;
                    }

                    toContinue = ProcessMarkerForDecode(marker, ref reader);
                }
            }
            finally
            {
                _scanDecoder?.Dispose();
                _scanDecoder = null;
            }
        }

        /// <summary>
        /// This routine is called when <see cref="Decode"/> meets a marker in the JPEG stream.
        /// </summary>
        /// <param name="marker">The JPEG marker.</param>
        /// <param name="reader">The JPEG reader.</param>
        /// <returns>True if <see cref="Decode"/> should continue reading.</returns>
        protected virtual bool ProcessMarkerForDecode(JpegMarker marker, ref JpegReader reader)
        {
            switch (marker)
            {
                case JpegMarker.StartOfFrame0:
                case JpegMarker.StartOfFrame1:
                case JpegMarker.StartOfFrame2:
                case JpegMarker.StartOfFrame3:
                case JpegMarker.StartOfFrame9:
                case JpegMarker.StartOfFrame10:
                    ProcessFrameHeader(ref reader, false, true);
                    _scanDecoder = JpegScanDecoder.Create(marker, this, _frameHeader.GetValueOrDefault());
                    break;
                case JpegMarker.StartOfFrame5:
                case JpegMarker.StartOfFrame6:
                case JpegMarker.StartOfFrame7:
                case JpegMarker.StartOfFrame11:
                case JpegMarker.StartOfFrame13:
                case JpegMarker.StartOfFrame14:
                case JpegMarker.StartOfFrame15:
                    ThrowInvalidDataException(reader.ConsumedByteCount, $"This type of JPEG stream is not supported ({marker}).");
                    break;
                case JpegMarker.DefineHuffmanTable:
                    ProcessDefineHuffmanTable(ref reader);
                    break;
                case JpegMarker.DefineArithmeticCodingConditioning:
                    ProcessDefineArithmeticCodingConditioning(ref reader);
                    break;
                case JpegMarker.DefineQuantizationTable:
                    ProcessDefineQuantizationTable(ref reader, loadQuantizationTables: true);
                    break;
                case JpegMarker.DefineRestartInterval:
                    ProcessDefineRestartInterval(ref reader);
                    break;
                case JpegMarker.StartOfScan:
                    if (_scanDecoder is null)
                    {
                        ThrowInvalidDataException(reader.ConsumedByteCount, "Scan header appears before frame header.");
                    }
                    JpegScanHeader scanHeader = ProcessScanHeader(ref reader, false);
                    _scanDecoder.ProcessScan(ref reader, scanHeader);
                    break;
                case JpegMarker.DefineRestart0:
                case JpegMarker.DefineRestart1:
                case JpegMarker.DefineRestart2:
                case JpegMarker.DefineRestart3:
                case JpegMarker.DefineRestart4:
                case JpegMarker.DefineRestart5:
                case JpegMarker.DefineRestart6:
                case JpegMarker.DefineRestart7:
                    break;
                case JpegMarker.EndOfImage:
                    return false;
                default:
                    ProcessOtherMarker(ref reader);
                    break;
            }

            return true;
        }

        /// <summary>
        /// Process scan data.
        /// </summary>
        /// <param name="reader">The JPEG reader.</param>
        /// <param name="scanHeader">The scan header.</param>
        public void ProcessScan(ref JpegReader reader, JpegScanHeader scanHeader)
        {
            using var scanDecoder = JpegScanDecoder.Create(StartOfFrame, this, GetFrameHeader());
            if (scanDecoder is null)
            {
                throw new NotSupportedException("This image type is not supported.");
            }
            scanDecoder.ProcessScan(ref reader, scanHeader);
        }

        private void ProcessDefineRestartInterval(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer) || buffer.Length < 2)
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
                return;
            }
            Span<byte> local = stackalloc byte[2];
            buffer.Slice(0, 2).CopyTo(local);
            _restartInterval = BinaryPrimitives.ReadUInt16BigEndian(local);
        }

        /// <summary>
        /// Get the restart interval.
        /// </summary>
        /// <returns>The restart interval</returns>
        public ushort GetRestartInterval() => _restartInterval;

        /// <summary>
        /// Set the restart interval.
        /// </summary>
        /// <param name="restartInterval">The restart interval</param>
        public void SetRestartInterval(int restartInterval)
        {
            if ((uint)restartInterval > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(restartInterval));
            }

            _restartInterval = (ushort)restartInterval;
        }

        private void ProcessDefineHuffmanTable(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
                return;
            }
            ProcessDefineHuffmanTable(buffer, reader.ConsumedByteCount - length);
        }

        private void ProcessDefineHuffmanTable(ReadOnlySequence<byte> segment, int currentOffset)
        {
            while (!segment.IsEmpty)
            {
                if (!JpegHuffmanDecodingTable.TryParse(segment, out JpegHuffmanDecodingTable? huffmanTable, out int bytesConsumed))
                {
                    ThrowInvalidDataException(currentOffset, "Failed to parse Huffman table.");
                    return;
                }
                segment = segment.Slice(bytesConsumed);
                currentOffset += bytesConsumed;
                SetHuffmanTable(huffmanTable);
            }
        }

        private void ProcessDefineArithmeticCodingConditioning(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
                return;
            }
            ProcessDefineArithmeticCodingConditioning(buffer, reader.ConsumedByteCount - length);
        }

        private void ProcessDefineArithmeticCodingConditioning(ReadOnlySequence<byte> segment, int currentOffset)
        {
            while (!segment.IsEmpty)
            {
                if (!JpegArithmeticDecodingTable.TryParse(segment, out JpegArithmeticDecodingTable? arithmeticTable, out int bytesConsumed))
                {
                    ThrowInvalidDataException(currentOffset, "Failed to parse arithmetic coding conditioning.");
                    return;
                }
                segment = segment.Slice(bytesConsumed);
                currentOffset += bytesConsumed;
                SetArithmeticTable(arithmeticTable);
            }
        }

        private void ProcessDefineQuantizationTable(ref JpegReader reader, bool loadQuantizationTables)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedByteCount, "Unexpected end of input data when reading segment content.");
                return;
            }
            if (loadQuantizationTables)
            {
                ProcessDefineQuantizationTable(buffer, reader.ConsumedByteCount - length);
            }
        }

        private void ProcessDefineQuantizationTable(ReadOnlySequence<byte> segment, int currentOffset)
        {
            while (!segment.IsEmpty)
            {
                if (!JpegQuantizationTable.TryParse(segment, out JpegQuantizationTable quantizationTable, out int bytesConsumed))
                {
                    ThrowInvalidDataException(currentOffset, "Failed to parse quantization table.");
                    return;
                }
                segment = segment.Slice(bytesConsumed);
                currentOffset += bytesConsumed;
                SetQuantizationTable(quantizationTable);
            }
        }

        /// <summary>
        /// Clear all the Huffman tables parsed from the JPEG stream.
        /// </summary>
        public void ClearHuffmanTable()
        {
            _huffmanTables?.Clear();
        }

        /// <summary>
        /// Clear all the arithmetic tables parsed from the JPEG stream.
        /// </summary>
        public void ClearArithmeticTable()
        {
            _arithmeticTables?.Clear();
        }

        /// <summary>
        /// Clear all the quantization tables parsed from the JPEG stream.
        /// </summary>
        public void ClearQuantizationTable()
        {
            _quantizationTables?.Clear();
        }

        /// <summary>
        /// Set the Huffman table.
        /// </summary>
        /// <param name="table">The Huffman table.</param>
        public void SetHuffmanTable(JpegHuffmanDecodingTable table)
        {
            if (table is null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            List<JpegHuffmanDecodingTable>? list = _huffmanTables;
            if (list is null)
            {
                list = _huffmanTables = new List<JpegHuffmanDecodingTable>(4);
            }
            for (int i = 0; i < list.Count; i++)
            {
                JpegHuffmanDecodingTable item = list[i];
                if (item.TableClass == table.TableClass && item.Identifier == table.Identifier)
                {
                    list[i] = table;
                    return;
                }
            }
            list.Add(table);
        }

        internal void SetArithmeticTable(JpegArithmeticDecodingTable table)
        {
            List<JpegArithmeticDecodingTable>? list = _arithmeticTables;
            if (list is null)
            {
                list = _arithmeticTables = new List<JpegArithmeticDecodingTable>(4);
            }
            for (int i = 0; i < list.Count; i++)
            {
                JpegArithmeticDecodingTable item = list[i];
                if (item.TableClass == table.TableClass && item.Identifier == table.Identifier)
                {
                    list[i] = table;
                    return;
                }
            }
            list.Add(table);
        }

        /// <summary>
        /// Set the quantization table.
        /// </summary>
        /// <param name="table">The quantization table.</param>
        public void SetQuantizationTable(JpegQuantizationTable table)
        {
            if (table.IsEmpty)
            {
                throw new ArgumentException("No actual quantization table is provided.", nameof(table));
            }
            List<JpegQuantizationTable>? list = _quantizationTables;
            if (list is null)
            {
                list = _quantizationTables = new List<JpegQuantizationTable>(2);
            }
            for (int i = 0; i < list.Count; i++)
            {
                JpegQuantizationTable item = list[i];
                if (item.Identifier == table.Identifier)
                {
                    list[i] = table;
                    return;
                }
            }
            list.Add(table);
        }

        /// <summary>
        /// Get the specified Huffman table.
        /// </summary>
        /// <param name="isDcTable">Whether the table is DC table.</param>
        /// <param name="identifier">The identifier of the Huffman table.</param>
        /// <returns>The Huffman table.</returns>
        public JpegHuffmanDecodingTable? GetHuffmanTable(bool isDcTable, byte identifier)
        {
            List<JpegHuffmanDecodingTable>? huffmanTables = _huffmanTables;
            if (huffmanTables is null)
            {
                return null;
            }
            int tableClass = isDcTable ? 0 : 1;
            foreach (JpegHuffmanDecodingTable item in huffmanTables)
            {
                if (item.TableClass == tableClass && item.Identifier == identifier)
                {
                    return item;
                }
            }
            return null;
        }

        JpegArithmeticDecodingTable? IJpegDecoder<TWriter>.GetArithmeticTable(bool isDcTable, byte identifier)
        {
            List<JpegArithmeticDecodingTable>? arithmeticTables = _arithmeticTables;
            if (arithmeticTables is null)
            {
                return null;
            }
            int tableClass = isDcTable ? 0 : 1;
            foreach (JpegArithmeticDecodingTable item in arithmeticTables)
            {
                if (item.TableClass == tableClass && item.Identifier == identifier)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// Get the specified quantization table.
        /// </summary>
        /// <param name="identifier">The identifier of the quantization table.</param>
        /// <returns>The quantization table.</returns>
        public JpegQuantizationTable GetQuantizationTable(byte identifier)
        {
            List<JpegQuantizationTable>? quantizationTables = _quantizationTables;
            if (quantizationTables is null)
            {
                return default;
            }
            foreach (JpegQuantizationTable item in quantizationTables)
            {
                if (item.Identifier == identifier)
                {
                    return item;
                }
            }
            return default;
        }

        /// <summary>
        /// Reset the decoder to the initial state.
        /// </summary>
        public void Reset()
        {
            ResetInput();
            ResetHeader();
            ResetTables();
            ResetOutputWriter();
        }

        /// <summary>
        /// Reset the input.
        /// </summary>
        public void ResetInput()
        {
            _inputBuffer = default;
        }

        /// <summary>
        /// Reset the JPEG headers.
        /// </summary>
        public void ResetHeader()
        {
            _frameHeader = null;
            _restartInterval = 0;
            _maxHorizontalSamplingFactor = null;
            _maxVerticalSamplingFactor = null;
        }

        /// <summary>
        /// Reset JPEG tables.
        /// </summary>
        public void ResetTables()
        {
            _huffmanTables?.Clear();
            _arithmeticTables?.Clear();
            _quantizationTables?.Clear();
        }

        TWriter IJpegDecoder<TWriter>.GetOutputWriter()
        {
            return _outputWriter ?? throw new InvalidOperationException();
        }

        /// <summary>
        /// Reset the output writer.
        /// </summary>
        public void ResetOutputWriter()
        {
            _outputWriter = default;
        }

    }
}
