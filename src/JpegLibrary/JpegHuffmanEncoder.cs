using JpegLibrary.Exp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary
{
    class JpegHuffmanEncoder
    {
        private int _minimumBufferSegmentSize;

        private JpegBlockInputReader? _input;
        private IBufferWriter<byte>? _output;

        private List<JpegQuantizationTable>? _quantizationTables;
        private List<JpegEncodingComponent>? _encodingComponents;
        private JpegEncodingTableManager<JpegHuffmanEncodingTable, JpegHuffmanEncodingTableBuilder> _encodingTables;

        public JpegHuffmanEncoder() : this(16384) { }

        public JpegHuffmanEncoder(int minimumBufferSegmentSize)
        {
            _minimumBufferSegmentSize = minimumBufferSegmentSize;
            _encodingTables = new JpegEncodingTableManager<JpegHuffmanEncodingTable, JpegHuffmanEncodingTableBuilder>();
        }

        public bool MostOptimalCoding { get; set; }
        protected int MinimumBufferSegmentSize => _minimumBufferSegmentSize;

        protected T CloneParameters<T>() where T : JpegHuffmanEncoder, new()
        {
            throw new NotImplementedException();
        }

        public MemoryPool<byte>? MemoryPool { get; set; }

        public void SetInputReader(JpegBlockInputReader inputReader)
        {
            _input = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        }

        public void SetOutput(IBufferWriter<byte> output)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public void SetQuantizationTable(JpegQuantizationTable table)
        {
            if (table.IsEmpty)
            {
                throw new ArgumentException("Quantization table is not initialized.", nameof(table));
            }
            if (table.ElementPrecision != 0)
            {
                throw new InvalidOperationException("Only baseline JPEG is supported.");
            }

            List<JpegQuantizationTable>? tables = _quantizationTables;
            if (tables is null)
            {
                _quantizationTables = tables = new List<JpegQuantizationTable>(2);
            }

            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i].Identifier == table.Identifier)
                {
                    tables[i] = table;
                    return;
                }
            }

            tables.Add(table);
        }

        private JpegQuantizationTable GetQuantizationTable(byte identifier)
        {
            if (_quantizationTables is null)
            {
                return default;
            }
            foreach (JpegQuantizationTable item in _quantizationTables)
            {
                if (item.Identifier == identifier)
                {
                    return item;
                }
            }
            return default;
        }

        public void AddComponent(byte componentIndex, byte quantizationTableIdentifier, byte horizontalSubsampling, byte verticalSubsampling)
        {
            if (horizontalSubsampling != 1 && horizontalSubsampling != 2 && horizontalSubsampling != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(horizontalSubsampling), "Subsampling factor can only be 1, 2 or 4.");
            }
            if (verticalSubsampling != 1 && verticalSubsampling != 2 && verticalSubsampling != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(verticalSubsampling), "Subsampling factor can only be 1, 2 or 4.");
            }

            List<JpegEncodingComponent>? components = _encodingComponents;
            if (components is null)
            {
                _encodingComponents = components = new List<JpegEncodingComponent>();
            }
            foreach (JpegEncodingComponent? item in components)
            {
                if (item.ComponentIndex == componentIndex)
                {
                    throw new ArgumentException("The component index is already used by another component.", nameof(componentIndex));
                }
            }

            JpegQuantizationTable quantizationTable = GetQuantizationTable(quantizationTableIdentifier);
            if (quantizationTable.IsEmpty)
            {
                throw new ArgumentException("Quantization table is not defined.", nameof(quantizationTableIdentifier));
            }

            var component = new JpegEncodingComponent
            {
                ComponentIndex = componentIndex,
                HorizontalSamplingFactor = horizontalSubsampling,
                VerticalSamplingFactor = verticalSubsampling,
                QuantizationTable = quantizationTable
            };
            components.Add(component);
        }

        /// <summary>
        /// Create a JPEG writer.
        /// </summary>
        /// <returns>The JPEG writer.</returns>
        protected JpegWriter CreateJpegWriter()
        {
            IBufferWriter<byte> output = _output ?? throw new InvalidOperationException("Output is not specified.");
            return new JpegWriter(output, _minimumBufferSegmentSize);
        }

        /// <summary>
        /// Write the StartOfImage marker.
        /// </summary>
        /// <param name="writer">The JPEG writer.</param>
        protected static void WriteStartOfImage(ref JpegWriter writer)
        {
            writer.WriteMarker(JpegMarker.StartOfImage);
        }

        /// <summary>
        /// Write quantization tables.
        /// </summary>
        /// <param name="writer">The JPEG writer.</param>
        protected void WriteQuantizationTables(ref JpegWriter writer)
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

        protected JpegFrameHeader WriteStartOfFrame(ref JpegWriter writer)
            => WriteStartOfFrame(ref writer, JpegMarker.StartOfFrame0);

        protected JpegFrameHeader WriteStartOfFrame(ref JpegWriter writer, JpegMarker startOfFrameMarker)
        {
            if (!startOfFrameMarker.IsStartOfFrameMarker())
            {
                throw new ArgumentOutOfRangeException(nameof(startOfFrameMarker));
            }
            JpegBlockInputReader? input = _input;
            if (input is null)
            {
                throw new InvalidOperationException("Input is not specified.");
            }

            List<JpegEncodingComponent>? encodingComponents = _encodingComponents;
            if (encodingComponents is null || encodingComponents.Count == 0)
            {
                throw new InvalidOperationException("No component is specified.");
            }
            JpegFrameComponentSpecificationParameters[] components = new JpegFrameComponentSpecificationParameters[encodingComponents.Count];
            for (int i = 0; i < encodingComponents.Count; i++)
            {
                JpegEncodingComponent thisComponent = encodingComponents[i];
                components[i] = new JpegFrameComponentSpecificationParameters((byte)(i + 1), thisComponent.HorizontalSamplingFactor, thisComponent.VerticalSamplingFactor, thisComponent.QuantizationTable.Identifier);
            }
            JpegFrameHeader frameHeader = new JpegFrameHeader(8, (ushort)input.Height, (ushort)input.Width, (byte)components.Length, components);

            writer.WriteMarker(startOfFrameMarker);
            byte bytesCount = frameHeader.BytesRequired;
            writer.WriteLength(bytesCount);
            Span<byte> buffer = writer.GetSpan(bytesCount);
            frameHeader.TryWrite(buffer, out _);
            writer.Advance(bytesCount);

            return frameHeader;
        }

        protected void WriteScanData(ref JpegWriter writer, JpegFrameHeader frameHeader, JpegScanComponentEncodingSpecification componentSpecification, JpegScanProgressiveEncodingSpecification progressiveSpecification, JpegBlockAllocator allocator)
        {
#if NO_FAST_SPAN
            JpegScanComponentEncodingSpecification[] componentSpecifications = new JpegScanComponentEncodingSpecification[1];
            componentSpecifications[0] = componentSpecification;
#else
            ReadOnlySpan<JpegScanComponentEncodingSpecification> componentSpecifications = MemoryMarshal.CreateReadOnlySpan(ref componentSpecification, 1);
#endif

            WriteScanData(ref writer, frameHeader, componentSpecifications, progressiveSpecification, allocator);
        }

        protected void WriteScanData(ref JpegWriter writer, JpegFrameHeader frameHeader, ReadOnlySpan<JpegScanComponentEncodingSpecification> componentSpecifications, JpegScanProgressiveEncodingSpecification progressiveSpecification, JpegBlockAllocator allocator)
        {
            int numOfComponents = componentSpecifications.Length;
            if (numOfComponents > 4)
            {
                throw new ArgumentException("Too many components.", nameof(componentSpecifications));
            }

            List<JpegEncodingComponent>? encodingComponents = _encodingComponents;
            if (encodingComponents is null || encodingComponents.Count == 0)
            {
                throw new InvalidOperationException("No component is specified.");
            }
            Span<JpegScanComponentSpecificationParameters> components = stackalloc JpegScanComponentSpecificationParameters[4];
            for (int i = 0; i < numOfComponents; i++)
            {
                JpegScanComponentEncodingSpecification componentSpecification = componentSpecifications[i];
                JpegEncodingComponent? component = null;
                for (int j = 0; j < encodingComponents.Count; j++)
                {
                    if (componentSpecification.ComponentIndex == encodingComponents[j].ComponentIndex)
                    {
                        component = encodingComponents[j];
                        break;
                    }
                }
                if (component is null)
                {
                    throw new InvalidOperationException($"Component {componentSpecification.ComponentIndex} not found.");
                }

                components[i] = new JpegScanComponentSpecificationParameters((byte)(i + 1), componentSpecification.DcTableIdentifier, componentSpecification.AcTableIdentifier);
            }

            // Compute maximum sampling factor and reset DC predictor
            int maxHorizontalSampling = 1;
            int maxVerticalSampling = 1;
            foreach (var currentComponent in components)
            {
                currentComponent.DcPredictor = 0;
                maxHorizontalSampling = Math.Max(maxHorizontalSampling, currentComponent.HorizontalSamplingFactor);
                maxVerticalSampling = Math.Max(maxVerticalSampling, currentComponent.VerticalSamplingFactor);
            }

            int mcusPerLine = (frameHeader.SamplesPerLine + 8 * maxHorizontalSampling - 1) / (8 * maxHorizontalSampling);
            int mcusPerColumn = (frameHeader.NumberOfLines + 8 * maxVerticalSampling - 1) / (8 * maxVerticalSampling);

            writer.EnterBitMode();

            for (int rowMcu = 0; rowMcu < mcusPerColumn; rowMcu++)
            {
                for (int colMcu = 0; colMcu < mcusPerLine; colMcu++)
                {
                    foreach (var component in components)
                    {
                        int index = component.Index;
                        int h = component.HorizontalSamplingFactor;
                        int v = component.VerticalSamplingFactor;
                        int offsetX = colMcu * h;
                        int offsetY = rowMcu * v;

                        for (int y = 0; y < v; y++)
                        {
                            int blockOffsetY = offsetY + y;
                            for (int x = 0; x < h; x++)
                            {
                                ref JpegBlock8x8 blockRef = ref allocator.GetBlockReference(index, offsetX + x, blockOffsetY);

                                EncodeBlock(ref writer, component, ref blockRef);
                            }
                        }
                    }
                }
            }

            // Padding
            writer.ExitBitMode();
        }

        protected void WriterStartOfScan(ref JpegWriter writer, JpegScanComponentEncodingSpecification componentSpecification, JpegScanProgressiveEncodingSpecification progressiveSpecification)
        {
#if NO_FAST_SPAN
            JpegScanComponentEncodingSpecification[] componentSpecifications = new JpegScanComponentEncodingSpecification[1];
            componentSpecifications[0] = componentSpecification;
#else
            ReadOnlySpan<JpegScanComponentEncodingSpecification> componentSpecifications = MemoryMarshal.CreateReadOnlySpan(ref componentSpecification, 1);
#endif
            WriteStartOfScan(ref writer, componentSpecifications, progressiveSpecification);
        }

        protected void WriteStartOfScan(ref JpegWriter writer, ReadOnlySpan<JpegScanComponentEncodingSpecification> componentSpecifications, JpegScanProgressiveEncodingSpecification progressiveSpecification)
        {
            int numOfComponents = componentSpecifications.Length;
            if (numOfComponents > 4)
            {
                throw new ArgumentException("Too many components.", nameof(componentSpecifications));
            }

            List<JpegEncodingComponent>? encodingComponents = _encodingComponents;
            if (encodingComponents is null || encodingComponents.Count == 0)
            {
                throw new InvalidOperationException("No component is specified.");
            }
            Span<JpegScanComponentSpecificationParameters> components = stackalloc JpegScanComponentSpecificationParameters[4];
            for (int i = 0; i < numOfComponents; i++)
            {
                JpegScanComponentEncodingSpecification componentSpecification = componentSpecifications[i];
                JpegEncodingComponent? component = null;
                for (int j = 0; j < encodingComponents.Count; j++)
                {
                    if (componentSpecification.ComponentIndex == encodingComponents[j].ComponentIndex)
                    {
                        component = encodingComponents[j];
                        break;
                    }
                }
                if (component is null)
                {
                    throw new InvalidOperationException($"Component {componentSpecification.ComponentIndex} not found.");
                }

                components[i] = new JpegScanComponentSpecificationParameters((byte)(i + 1), componentSpecification.DcTableIdentifier, componentSpecification.AcTableIdentifier);
            }

            var scanHeader = new JpegScanHeaderWriter(components.Slice(0, numOfComponents), progressiveSpecification.StartOfSpectralSelection, progressiveSpecification.EndOfSpectralSelection, progressiveSpecification.SuccessiveApproximationBitPositionHigh, progressiveSpecification.SuccessiveApproximationBitPositionLow);

            writer.WriteMarker(JpegMarker.StartOfScan);
            byte bytesCount = scanHeader.BytesRequired;
            writer.WriteLength(bytesCount);
            Span<byte> buffer = writer.GetSpan(bytesCount);
            scanHeader.TryWrite(buffer, out _);
            writer.Advance(bytesCount);
        }

        /// <summary>
        /// Encode each block and save the coefficients.
        /// </summary>
        /// <param name="allocator">The coefficient allocator.</param>
        protected void TransformBlocks(JpegBlockAllocator allocator)
        {
            JpegBlockInputReader inputReader = _input ?? throw new InvalidOperationException("Input is not specified.");
            List<JpegEncodingComponent>? components = _encodingComponents;
            if (components is null || components.Count == 0)
            {
                throw new InvalidOperationException("No component is specified.");
            }

            // Compute maximum sampling factor
            int maxHorizontalSampling = 1;
            int maxVerticalSampling = 1;
            foreach (JpegEncodingComponent currentComponent in components)
            {
                maxHorizontalSampling = Math.Max(maxHorizontalSampling, currentComponent.HorizontalSamplingFactor);
                maxVerticalSampling = Math.Max(maxVerticalSampling, currentComponent.VerticalSamplingFactor);
            }

            JpegCoefficientsTransformContext[] contexts = new JpegCoefficientsTransformContext[components.Count];
            for (int i = 0; i < components.Count; i++)
            {
                JpegEncodingComponent currentComponent = components[i];
                contexts[i] = new JpegCoefficientsTransformContext(currentComponent.HorizontalSamplingFactor, currentComponent.VerticalSamplingFactor, maxHorizontalSampling, maxVerticalSampling, currentComponent.QuantizationTable);
            }

            int mcusPerLine = (inputReader.Width + 8 * maxHorizontalSampling - 1) / (8 * maxHorizontalSampling);
            int mcusPerColumn = (inputReader.Height + 8 * maxVerticalSampling - 1) / (8 * maxVerticalSampling);
            const int levelShift = 1 << (8 - 1);

            Unsafe.SkipInit(out JpegBlock8x8F inputFBuffer);
            Unsafe.SkipInit(out JpegBlock8x8F outputFBuffer);
            Unsafe.SkipInit(out JpegBlock8x8F tempFBuffer);

            for (int rowMcu = 0; rowMcu < mcusPerColumn; rowMcu++)
            {
                for (int colMcu = 0; colMcu < mcusPerLine; colMcu++)
                {
                    for (int i = 0; i < contexts.Length; i++)
                    {
                        JpegCoefficientsTransformContext context = contexts[i];
                        int h = context.HorizontalSamplingFactor;
                        int v = context.VerticalSamplingFactor;
                        int hs = context.HorizontalSubsamplingFactor;
                        int vs = context.VerticalSubsamplingFactor;
                        int offsetX = colMcu * h;
                        int offsetY = rowMcu * v;

                        for (int y = 0; y < v; y++)
                        {
                            int blockOffsetY = offsetY + y;
                            for (int x = 0; x < h; x++)
                            {
                                ref JpegBlock8x8 blockRef = ref allocator.GetBlockReference(i, offsetX + x, blockOffsetY);

                                // Read Block
                                ReadBlock(inputReader, out blockRef, i, (offsetX + x) * 8 * hs, blockOffsetY * 8 * vs, hs, vs);

                                // Level shift
                                ShiftDataLevel(ref blockRef, ref inputFBuffer, levelShift);

                                // FDCT
                                FastFloatingPointDCT.TransformFDCT(ref inputFBuffer, ref outputFBuffer, ref tempFBuffer);

                                // ZigZagAndQuantize
                                ZigZagAndQuantizeBlock(context.QuantizationTable, ref outputFBuffer, ref blockRef);
                            }
                        }
                    }
                }
            }
        }

        private static void ReadBlock(JpegBlockInputReader inputReader, out JpegBlock8x8 block, int componentIndex, int x, int y, int h, int v)
        {
            ref short blockRef = ref Unsafe.As<JpegBlock8x8, short>(ref block);

            if (h == 1 && v == 1)
            {
                inputReader.ReadBlock(ref blockRef, componentIndex, x, y);
                return;
            }

            ReadBlockWithSubsample(inputReader, ref blockRef, componentIndex, x, y, h, v);
        }

        private static void ReadBlockWithSubsample(JpegBlockInputReader inputReader, ref short blockRef, int componentIndex, int x, int y, int horizontalSubsampling, int verticalSubsampling)
        {
            Unsafe.SkipInit(out JpegBlock8x8 temp);

            ref short tempRef = ref Unsafe.As<JpegBlock8x8, short>(ref temp);

            int hShift = JpegMathHelper.Log2((uint)horizontalSubsampling);
            int vShift = JpegMathHelper.Log2((uint)verticalSubsampling);
            int hBlockShift = 3 - hShift;
            int vBlockShift = 3 - vShift;

            for (int v = 0; v < verticalSubsampling; v++)
            {
                for (int h = 0; h < horizontalSubsampling; h++)
                {
                    inputReader.ReadBlock(ref tempRef, componentIndex, x + 8 * h, y + 8 * v);

                    CopySubsampleBlock(ref tempRef, ref blockRef, h << hBlockShift, v << vBlockShift, hShift, vShift);
                }
            }

            int totalShift = hShift + vShift;
            if (totalShift > 0)
            {
                int delta = 1 << (totalShift - 1);
                for (int i = 0; i < 64; i++)
                {
                    Unsafe.Add(ref blockRef, i) = (short)((Unsafe.Add(ref blockRef, i) + delta) >> totalShift);
                }
            }
        }

        private static void CopySubsampleBlock(ref short sourceRef, ref short destinationRef, int blockOffsetX, int blockOffsetY, int hShift, int vShift)
        {
            for (int y = 0; y < 8; y++)
            {
                ref short sourceRowRef = ref Unsafe.Add(ref sourceRef, y * 8);
                ref short destinationRowRef = ref Unsafe.Add(ref destinationRef, (blockOffsetY + (y >> vShift)) * 8 + blockOffsetX);
                for (int x = 0; x < 8; x++)
                {
                    Unsafe.Add(ref destinationRowRef, x >> hShift) += Unsafe.Add(ref sourceRowRef, x);
                }
            }
        }

        private static void ShiftDataLevel(ref JpegBlock8x8 source, ref JpegBlock8x8F destination, int levelShift)
        {
            ref short sourceRef = ref Unsafe.As<JpegBlock8x8, short>(ref source);
            ref float destinationRef = ref Unsafe.As<JpegBlock8x8F, float>(ref destination);

            for (int i = 0; i < 64; i++)
            {
                Unsafe.Add(ref destinationRef, i) = Unsafe.Add(ref sourceRef, i) - levelShift;
            }
        }

        private static void ZigZagAndQuantizeBlock(JpegQuantizationTable quantizationTable, ref JpegBlock8x8F input, ref JpegBlock8x8 output)
        {
            Debug.Assert(!quantizationTable.IsEmpty);

            ref ushort elementRef = ref MemoryMarshal.GetReference(quantizationTable.Elements);
            ref float sourceRef = ref Unsafe.As<JpegBlock8x8F, float>(ref input);
            ref short destinationRef = ref Unsafe.As<JpegBlock8x8, short>(ref output);

            for (int i = 0; i < 64; i++)
            {
                float coefficient = Unsafe.Add(ref sourceRef, JpegZigZag.InternalBufferIndexToBlock(i));
                ushort element = Unsafe.Add(ref elementRef, i);
                Unsafe.Add(ref destinationRef, i) = JpegMathHelper.RoundToInt16(coefficient / element);
            }
        }


        private static int CountZeros(ref long x)
        {
            int result = JpegMathHelper.TrailingZeroCount((ulong)x);
            x >>= result;
            return result;
        }

    }
}
