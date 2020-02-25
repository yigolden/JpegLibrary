#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace JpegLibrary
{
    public class JpegOptimizer
    {
        private readonly int _minimumBufferSegmentSize;

        private ReadOnlySequence<byte> _inputBuffer;

        private JpegFrameHeader? _frameHeader;
        private ushort _restartInterval;

        private List<JpegQuantizationTable>? _quantizationTables;
        private List<JpegHuffmanDecodingTable>? _huffmanTables;
        private JpegHuffmanEncodingTableCollection _encodingTables;

        private IBufferWriter<byte>? _output;

        public JpegOptimizer() : this(4096) { }

        public JpegOptimizer(int minimumBufferSegmentSize)
        {
            _minimumBufferSegmentSize = minimumBufferSegmentSize;
        }

        public void SetInput(ReadOnlyMemory<byte> inputBuffer)
            => SetInput(new ReadOnlySequence<byte>(inputBuffer));

        public void SetInput(ReadOnlySequence<byte> inputBuffer)
        {
            _inputBuffer = inputBuffer;

            _frameHeader = null;
            _restartInterval = 0;
        }

        public void Scan()
        {
            if (_inputBuffer.IsEmpty)
            {
                throw new InvalidOperationException("Input buffer is not specified.");
            }

            JpegReader reader = new JpegReader(_inputBuffer);

            // Reset frame header
            _frameHeader = default;

            bool scanRead = false;
            bool endOfImageReached = false;
            while (!endOfImageReached && !reader.IsEmpty)
            {
                // Read next marker
                if (!reader.TryReadMarker(out JpegMarker marker))
                {
                    ThrowInvalidDataException(reader.ConsumedBytes, "No marker found.");
                    return;
                }

                switch (marker)
                {
                    case JpegMarker.StartOfImage:
                        break;
                    case JpegMarker.StartOfFrame0:
                        ProcessFrameHeader(ref reader, false, false);
                        break;
                    case JpegMarker.StartOfFrame1:
                        ProcessFrameHeader(ref reader, false, false);
                        break;
                    case JpegMarker.StartOfFrame2:
                        ProcessFrameHeader(ref reader, false, false);
                        throw new InvalidDataException("Progressive JPEG is not supported currently.");
                    case JpegMarker.StartOfFrame3:
                    case JpegMarker.StartOfFrame5:
                    case JpegMarker.StartOfFrame6:
                    case JpegMarker.StartOfFrame7:
                    case JpegMarker.StartOfFrame9:
                    case JpegMarker.StartOfFrame10:
                    case JpegMarker.StartOfFrame11:
                    case JpegMarker.StartOfFrame13:
                    case JpegMarker.StartOfFrame14:
                    case JpegMarker.StartOfFrame15:
                        ThrowInvalidDataException(reader.ConsumedBytes, $"This type of JPEG stream is not supported ({marker}).");
                        return;
                    case JpegMarker.DefineHuffmanTable:
                        ProcessDefineHuffmanTable(ref reader);
                        break;
                    case JpegMarker.DefineQuantizationTable:
                        ProcessDefineQuantizationTable(ref reader);
                        break;
                    case JpegMarker.DefineRestartInterval:
                        ProcessDefineRestartInterval(ref reader);
                        break;
                    case JpegMarker.StartOfScan:
                        JpegScanHeader scanHeader = ProcessScanHeader(ref reader, false);
                        ProcessScanBaseline(ref reader, scanHeader);
                        scanRead = true;
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
                        endOfImageReached = true;
                        break;
                    default:
                        ProcessOtherMarker(ref reader);
                        break;
                }
            }

            if (!scanRead)
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "No image data is read.");
                return;
            }
        }

        private void ProcessFrameHeader(ref JpegReader reader, bool metadataOnly, bool overrideAllowed)
        {
            // Read length field
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
                return;
            }
            if (!JpegFrameHeader.TryParse(buffer, metadataOnly, out JpegFrameHeader frameHeader, out int bytesConsumed))
            {
                ThrowInvalidDataException(reader.ConsumedBytes - length + bytesConsumed, "Failed to parse frame header.");
                return;
            }
            if (!overrideAllowed && _frameHeader.HasValue)
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Multiple frame is not supported.");
                return;
            }
            _frameHeader = frameHeader;
        }

        private void ProcessOtherMarker(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryAdvance(length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data reached.");
                return;
            }
        }

        private JpegScanHeader ProcessScanHeader(ref JpegReader reader, bool metadataOnly)
        {

            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
            }
            if (!JpegScanHeader.TryParse(buffer, metadataOnly, out JpegScanHeader scanHeader, out int bytesConsumed))
            {
                ThrowInvalidDataException(reader.ConsumedBytes - length + bytesConsumed, "Failed to parse scan header.");
            }
            return scanHeader;
        }

        private void ProcessDefineRestartInterval(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer) || buffer.Length < 2)
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
                return;
            }
            Span<byte> local = stackalloc byte[2];
            buffer.Slice(0, 2).CopyTo(local);
            _restartInterval = BinaryPrimitives.ReadUInt16BigEndian(local);
        }


        private void ProcessDefineHuffmanTable(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
                return;
            }
            ProcessDefineHuffmanTable(buffer, reader.ConsumedBytes - length);
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


        private void ProcessDefineQuantizationTable(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
                return;
            }
            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
                return;
            }
            ProcessDefineQuantizationTable(buffer, reader.ConsumedBytes - length);
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


        private void SetHuffmanTable(JpegHuffmanDecodingTable table)
        {
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

        private JpegHuffmanDecodingTable? GetHuffmanTable(bool isDcTable, byte identifier)
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

        internal void SetQuantizationTable(JpegQuantizationTable table)
        {
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

        private void ProcessScanBaseline(ref JpegReader reader, JpegScanHeader scanHeader)
        {
            JpegFrameHeader frameHeader = _frameHeader.GetValueOrDefault();

            if (scanHeader.Components is null)
            {
                throw new InvalidOperationException();
            }

            // Compute maximum sampling factor
            byte maxHorizontalSampling = 1;
            byte maxVerticalSampling = 1;
            foreach (JpegFrameComponentSpecificationParameters currentFrameComponent in frameHeader.Components!)
            {
                maxHorizontalSampling = Math.Max(maxHorizontalSampling, currentFrameComponent.HorizontalSamplingFactor);
                maxVerticalSampling = Math.Max(maxVerticalSampling, currentFrameComponent.VerticalSamplingFactor);
            }

            // Prepare table builder
            using var tableBuilderCollection = new JpegHuffmanEncodingTableBuilderCollection();

            // Resolve each component
            JpegTranscodeComponent[] components = new JpegTranscodeComponent[scanHeader.NumberOfComponents];

            for (int i = 0; i < scanHeader.NumberOfComponents; i++)
            {
                JpegScanComponentSpecificationParameters scanComponenet = scanHeader.Components[i];
                int componentIndex = 0;
                JpegFrameComponentSpecificationParameters? frameComponent = null;

                for (int j = 0; j < frameHeader.NumberOfComponents; j++)
                {
                    JpegFrameComponentSpecificationParameters currentFrameComponent = frameHeader.Components[j];
                    if (scanComponenet.ScanComponentSelector == currentFrameComponent.Identifier)
                    {
                        componentIndex = j;
                        frameComponent = currentFrameComponent;
                    }
                }
                if (frameComponent is null)
                {
                    throw new InvalidDataException();
                }
                components[i] = new JpegTranscodeComponent
                {
                    ComponentIndex = componentIndex,
                    HorizontalSamplingFactor = frameComponent.GetValueOrDefault().HorizontalSamplingFactor,
                    VerticalSamplingFactor = frameComponent.GetValueOrDefault().VerticalSamplingFactor,
                    DcTable = GetHuffmanTable(true, scanComponenet.DcEntropyCodingTableSelector),
                    AcTable = GetHuffmanTable(false, scanComponenet.AcEntropyCodingTableSelector),
                    DcTableBuilder = tableBuilderCollection.GetOrCreateTableBuilder(true, scanComponenet.DcEntropyCodingTableSelector),
                    AcTableBuilder = tableBuilderCollection.GetOrCreateTableBuilder(false, scanComponenet.AcEntropyCodingTableSelector)
                };
            }

            // Prepare
            int mcusPerLine = (frameHeader.SamplesPerLine + 8 * maxHorizontalSampling - 1) / (8 * maxHorizontalSampling);
            int mcusPerColumn = (frameHeader.NumberOfLines + 8 * maxVerticalSampling - 1) / (8 * maxVerticalSampling);
            JpegBitReader bitReader = new JpegBitReader(reader.RemainingBytes);
            int mcusBeforeRestart = _restartInterval;

            for (int rowMcu = 0; rowMcu < mcusPerColumn; rowMcu++)
            {
                for (int colMcu = 0; colMcu < mcusPerLine; colMcu++)
                {
                    foreach (JpegTranscodeComponent component in components)
                    {
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        for (int y = 0; y < v; y++)
                        {
                            for (int x = 0; x < h; x++)
                            {
                                ProcessBlockBaseline(ref bitReader, component);
                            }
                        }
                    }

                    if (_restartInterval > 0 && (--mcusBeforeRestart) == 0)
                    {
                        bitReader.AdvanceAlignByte();

                        JpegMarker marker = bitReader.TryReadMarker();
                        if (marker == JpegMarker.EndOfImage)
                        {
                            int bytesConsumedEoi = reader.RemainingByteCount - bitReader.RemainingBits / 8;
                            reader.TryAdvance(bytesConsumedEoi - 2);
                            return;
                        }
                        if (!marker.IsRestartMarker())
                        {
                            throw new InvalidOperationException("Expect restart marker.");
                        }

                        mcusBeforeRestart = _restartInterval;

                    }
                }
            }

            bitReader.AdvanceAlignByte();
            int bytesConsumed = reader.RemainingByteCount - bitReader.RemainingBits / 8;
            if (bitReader.TryPeekMarker() != 0)
            {
                if (!bitReader.TryPeekMarker().IsRestartMarker())
                {
                    bytesConsumed -= 2;
                }
            }
            reader.TryAdvance(bytesConsumed);

            // Generate new huffman table
            _encodingTables = tableBuilderCollection.BuildTables();
        }

        private static void ProcessBlockBaseline(ref JpegBitReader reader, JpegTranscodeComponent component)
        {
            Debug.Assert(!(component.DcTable is null));
            Debug.Assert(!(component.DcTableBuilder is null));
            Debug.Assert(!(component.AcTable is null));
            Debug.Assert(!(component.AcTableBuilder is null));

            // DC
            int t = DecodeHuffmanCode(ref reader, component.DcTable!);
            component.DcTableBuilder!.IncrementCodeCount(t);
            if (t != 0)
            {
                Receive(ref reader, t);
            }

            // AC
            for (int i = 1; i < 64;)
            {
                int s = DecodeHuffmanCode(ref reader, component.AcTable!);
                component.AcTableBuilder!.IncrementCodeCount(s);

                int r = s >> 4;
                s &= 15;

                if (s != 0)
                {
                    i += r;
                    i++;
                    Receive(ref reader, s);
                }
                else
                {
                    if (r == 0)
                    {
                        break;
                    }

                    i += 16;
                }
            }
        }



        private static int DecodeHuffmanCode(ref JpegBitReader reader, JpegHuffmanDecodingTable table)
        {
            int bits = reader.PeekBits(16, out int bitsRead);
            JpegHuffmanDecodingTable.Entry entry = table.Lookup(bits);
            bitsRead = Math.Min(entry.CodeSize, bitsRead);
            _ = reader.TryAdvanceBits(bitsRead, out _);
            return entry.CodeValue;
        }


        private static int Receive(ref JpegBitReader reader, int length)
        {
            Debug.Assert(length > 0);
            if (!reader.TryReadBits(length, out int value, out bool isMarkerEncountered))
            {
                if (isMarkerEncountered)
                {
                    ThrowInvalidDataException("Expect raw data from bit stream. Yet a marker is encountered.");
                }
                ThrowInvalidDataException("The bit stream ended prematurely.");
            }

            return value;
        }

        public void SetOutput(IBufferWriter<byte> output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void Optimize(bool strip = true)
        {
            if (_encodingTables.IsEmpty)
            {
                throw new InvalidOperationException();
            }

            IBufferWriter<byte> bufferWriter = _output ?? throw new InvalidOperationException();

            JpegReader reader = new JpegReader(_inputBuffer);
            var writer = new JpegWriter(bufferWriter, _minimumBufferSegmentSize);

            bool eoiReached = false;
            bool huffmanTableWritten = false;
            bool quantizationTableWritten = false;
            while (!eoiReached && !reader.IsEmpty)
            {
                if (!reader.TryReadMarker(out JpegMarker marker))
                {
                    ThrowInvalidDataException(reader.ConsumedBytes, "No marker found.");
                    return;
                }

                switch (marker)
                {
                    case JpegMarker.StartOfImage:
                        writer.WriteMarker(marker);
                        break;
                    case JpegMarker.App0:
                    case JpegMarker.StartOfFrame0:
                    case JpegMarker.StartOfFrame1:
                        writer.WriteMarker(marker);
                        CopyMarkerData(ref reader, ref writer);
                        break;
                    case JpegMarker.StartOfFrame2:
                        ProcessFrameHeader(ref reader, false, true);
                        throw new InvalidDataException("Progressive JPEG is not supported currently.");
                    case JpegMarker.StartOfFrame3:
                    case JpegMarker.StartOfFrame5:
                    case JpegMarker.StartOfFrame6:
                    case JpegMarker.StartOfFrame7:
                    case JpegMarker.StartOfFrame9:
                    case JpegMarker.StartOfFrame10:
                    case JpegMarker.StartOfFrame11:
                    case JpegMarker.StartOfFrame13:
                    case JpegMarker.StartOfFrame14:
                    case JpegMarker.StartOfFrame15:
                        ThrowInvalidDataException(reader.ConsumedBytes, $"This type of JPEG stream is not supported ({marker}).");
                        return;
                    case JpegMarker.DefineHuffmanTable:
                        if (!huffmanTableWritten)
                        {
                            WriteHuffmanTables(ref writer);
                            huffmanTableWritten = true;
                        }
                        break;
                    case JpegMarker.DefineQuantizationTable:
                        if (!quantizationTableWritten)
                        {
                            WriteQuantizationTables(ref writer);
                            quantizationTableWritten = true;
                        }
                        break;
                    case JpegMarker.StartOfScan:
                        writer.WriteMarker(marker);
                        ReadOnlySequence<byte> buffer = CopyMarkerData(ref reader, ref writer);
                        if (!JpegScanHeader.TryParse(buffer, false, out JpegScanHeader scanHeader, out _))
                        {
                            ThrowInvalidDataException(reader.ConsumedBytes - (int)buffer.Length, "Failed to parse scan header.");
                        }
                        CopyScanBaseline(ref reader, ref writer, scanHeader);
                        break;
                    case JpegMarker.DefineRestart0:
                    case JpegMarker.DefineRestart1:
                    case JpegMarker.DefineRestart2:
                    case JpegMarker.DefineRestart3:
                    case JpegMarker.DefineRestart4:
                    case JpegMarker.DefineRestart5:
                    case JpegMarker.DefineRestart6:
                    case JpegMarker.DefineRestart7:
                        writer.WriteMarker(marker);
                        break;
                    case JpegMarker.EndOfImage:
                        writer.WriteMarker(JpegMarker.EndOfImage);
                        eoiReached = true;
                        break;
                    default:
                        if (strip)
                        {
                            SkipMarkerData(ref reader);
                        }
                        else
                        {
                            writer.WriteMarker(marker);
                            CopyMarkerData(ref reader, ref writer);
                        }
                        break;
                }
            }

            writer.Flush();
        }

        private static void SkipMarkerData(ref JpegReader reader)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
            }
            if (!reader.TryAdvance(length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
            }
        }

        private static ReadOnlySequence<byte> CopyMarkerData(ref JpegReader reader, ref JpegWriter writer)
        {
            if (!reader.TryReadLength(out ushort length))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment length.");
            }

            if (!reader.TryReadBytes(length, out ReadOnlySequence<byte> buffer))
            {
                ThrowInvalidDataException(reader.ConsumedBytes, "Unexpected end of input data when reading segment content.");
            }
            writer.WriteLength(length);
            writer.WriteBytes(buffer);
            return buffer;
        }

        private void WriteHuffmanTables(ref JpegWriter writer)
        {
            if (_encodingTables.IsEmpty)
            {
                throw new InvalidOperationException();
            }

            writer.WriteMarker(JpegMarker.DefineHuffmanTable);
            ushort totalByteCoubt = _encodingTables.GetTotalBytesRequired();
            writer.WriteLength(totalByteCoubt);
            _encodingTables.Write(ref writer);
        }

        private void WriteQuantizationTables(ref JpegWriter writer)
        {
            List<JpegQuantizationTable>? quantizationTables = _quantizationTables;
            if (quantizationTables is null)
            {
                throw new InvalidOperationException();
            }

            writer.WriteMarker(JpegMarker.DefineQuantizationTable);

            ushort totalByteCount = 0;
            foreach (JpegQuantizationTable table in quantizationTables)
            {
                totalByteCount += table.BytesRequired;
            }

            writer.WriteLength(totalByteCount);

            foreach (JpegQuantizationTable table in quantizationTables)
            {
                Span<byte> buffer = writer.GetSpan(table.BytesRequired);
                table.TryWrite(buffer, out int bytesWritten);
                writer.Advance(bytesWritten);
            }
        }

        private void CopyScanBaseline(ref JpegReader reader, ref JpegWriter writer, JpegScanHeader scanHeader)
        {
            JpegFrameHeader frameHeader = _frameHeader.GetValueOrDefault();

            if (scanHeader.Components is null)
            {
                throw new InvalidOperationException();
            }

            // Compute maximum sampling factor
            byte maxHorizontalSampling = 1;
            byte maxVerticalSampling = 1;
            foreach (JpegFrameComponentSpecificationParameters currentFrameComponent in frameHeader.Components!)
            {
                maxHorizontalSampling = Math.Max(maxHorizontalSampling, currentFrameComponent.HorizontalSamplingFactor);
                maxVerticalSampling = Math.Max(maxVerticalSampling, currentFrameComponent.VerticalSamplingFactor);
            }

            // Resolve each component
            JpegTranscodeComponent[] components = new JpegTranscodeComponent[scanHeader.NumberOfComponents];

            for (int i = 0; i < scanHeader.NumberOfComponents; i++)
            {
                JpegScanComponentSpecificationParameters scanComponenet = scanHeader.Components[i];
                int componentIndex = 0;
                JpegFrameComponentSpecificationParameters? frameComponent = null;

                for (int j = 0; j < frameHeader.NumberOfComponents; j++)
                {
                    JpegFrameComponentSpecificationParameters currentFrameComponent = frameHeader.Components[j];
                    if (scanComponenet.ScanComponentSelector == currentFrameComponent.Identifier)
                    {
                        componentIndex = j;
                        frameComponent = currentFrameComponent;
                    }
                }
                if (frameComponent is null)
                {
                    throw new InvalidDataException();
                }
                components[i] = new JpegTranscodeComponent
                {
                    ComponentIndex = componentIndex,
                    HorizontalSamplingFactor = frameComponent.GetValueOrDefault().HorizontalSamplingFactor,
                    VerticalSamplingFactor = frameComponent.GetValueOrDefault().VerticalSamplingFactor,
                    DcTable = GetHuffmanTable(true, scanComponenet.DcEntropyCodingTableSelector),
                    AcTable = GetHuffmanTable(false, scanComponenet.AcEntropyCodingTableSelector),
                    DcEncodingTable = _encodingTables.GetTable(true, scanComponenet.DcEntropyCodingTableSelector),
                    AcEncodingTable = _encodingTables.GetTable(false, scanComponenet.AcEntropyCodingTableSelector)
                };
            }

            // Prepare
            int mcusPerLine = (frameHeader.SamplesPerLine + 8 * maxHorizontalSampling - 1) / (8 * maxHorizontalSampling);
            int mcusPerColumn = (frameHeader.NumberOfLines + 8 * maxVerticalSampling - 1) / (8 * maxVerticalSampling);
            JpegBitReader bitReader = new JpegBitReader(reader.RemainingBytes);
            int mcusBeforeRestart = _restartInterval;

            bool eoiReached = false;
            writer.EnterBitMode();
            for (int rowMcu = 0; rowMcu < mcusPerColumn && !eoiReached; rowMcu++)
            {
                for (int colMcu = 0; colMcu < mcusPerLine && !eoiReached; colMcu++)
                {
                    foreach (JpegTranscodeComponent component in components)
                    {
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;

                        for (int y = 0; y < v; y++)
                        {
                            for (int x = 0; x < h; x++)
                            {
                                CopyBlockBaseline(ref bitReader, ref writer, component);
                            }
                        }
                    }

                    if (_restartInterval > 0 && (--mcusBeforeRestart) == 0)
                    {
                        bitReader.AdvanceAlignByte();

                        JpegMarker marker = bitReader.TryReadMarker();
                        if (marker == JpegMarker.EndOfImage)
                        {
                            eoiReached = true;
                            break;
                        }
                        if (!marker.IsRestartMarker())
                        {
                            throw new InvalidOperationException("Expect restart marker.");
                        }

                        mcusBeforeRestart = _restartInterval;

                        writer.ExitBitMode();
                        writer.WriteMarker(marker);
                        writer.EnterBitMode();
                    }
                }
            }

            bitReader.AdvanceAlignByte();
            writer.ExitBitMode();

            int bytesConsumed = reader.RemainingByteCount - bitReader.RemainingBits / 8;
            if (eoiReached)
            {
                bytesConsumed -= 2;
            }
            else if (bitReader.TryPeekMarker() != 0)
            {
                if (!bitReader.TryPeekMarker().IsRestartMarker())
                {
                    bytesConsumed -= 2;
                }
            }
            reader.TryAdvance(bytesConsumed);
        }

        private static void CopyBlockBaseline(ref JpegBitReader reader, ref JpegWriter writer, JpegTranscodeComponent component)
        {
            Debug.Assert(!(component.DcTable is null));
            Debug.Assert(!(component.DcEncodingTable is null));
            Debug.Assert(!(component.AcTable is null));
            Debug.Assert(!(component.AcEncodingTable is null));

            // DC
            int symbol = DecodeHuffmanCode(ref reader, component.DcTable!);
            component.DcEncodingTable!.GetCode(symbol, out ushort code, out int codeLength);
            writer.WriteBits(code, codeLength);
            if (symbol != 0)
            {
                int received = Receive(ref reader, symbol);
                writer.WriteBits((uint)received, symbol);
            }

            // AC
            for (int i = 1; i < 64;)
            {
                symbol = DecodeHuffmanCode(ref reader, component.AcTable!);
                component.AcEncodingTable!.GetCode(symbol, out code, out codeLength);
                writer.WriteBits(code, codeLength);

                int r = symbol >> 4;
                symbol &= 15;

                if (symbol != 0)
                {
                    i += r + 1;
                    int received = Receive(ref reader, symbol);
                    writer.WriteBits((uint)received, symbol);
                }
                else
                {
                    if (r == 0)
                    {
                        break;
                    }

                    i += 16;
                }
            }
        }

        private static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException(message);
        }
        private static void ThrowInvalidDataException(int offset, string message)
        {
            throw new InvalidDataException($"Failed to decode JPEG data at offset {offset}. {message}");
        }


    }

}
