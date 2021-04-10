﻿#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary.ScanDecoder
{
    internal abstract class JpegScanDecoder : IDisposable
    {
        public abstract void ProcessScan(ref JpegReader reader, JpegScanHeader scanHeader);

        public abstract void Dispose();

        public static JpegScanDecoder? Create<TWriter>(JpegMarker sofMarker, IJpegDecoder<TWriter> decoder, JpegFrameHeader header) where TWriter: notnull, IJpegBlockOutputWriter
        {
            switch (sofMarker)
            {
                case JpegMarker.StartOfFrame0:
                case JpegMarker.StartOfFrame1:
                    return new JpegHuffmanBaselineScanDecoder<TWriter>(decoder, header);
                case JpegMarker.StartOfFrame2:
                    return new JpegHuffmanProgressiveScanDecoder<TWriter>(decoder, header);
                case JpegMarker.StartOfFrame3:
                    return new JpegHuffmanLosslessScanDecoder<TWriter>(decoder, header);
                case JpegMarker.StartOfFrame9:
                    return new JpegArithmeticSequentialScanDecoder<TWriter>(decoder, header);
                case JpegMarker.StartOfFrame10:
                    return new JpegArithmeticProgressiveScanDecoder<TWriter>(decoder, header);
                default:
                    return null;
            }
        }

        [DoesNotReturn]
        protected static void ThrowInvalidDataException(string message)
        {
            throw new InvalidDataException("Failed to decode JPEG data. " + message);
        }

        [DoesNotReturn]
        protected static void ThrowInvalidDataException(int offset, string message)
        {
            throw new InvalidDataException($"Failed to decode JPEG data at offset {offset}. {message}");
        }

        protected static void DequantizeBlockAndUnZigZag(JpegQuantizationTable quantizationTable, ref JpegBlock8x8 input, ref JpegBlock8x8F output)
        {
            Debug.Assert(!quantizationTable.IsEmpty);

            ref ushort elementRef = ref MemoryMarshal.GetReference(quantizationTable.Elements);
            ref short sourceRef = ref Unsafe.As<JpegBlock8x8, short>(ref input);
            ref float destinationRef = ref Unsafe.As<JpegBlock8x8F, float>(ref output);

            for (int i = 0; i < 64; i++)
            {
                Unsafe.Add(ref destinationRef, JpegZigZag.InternalBufferIndexToBlock(i)) = Unsafe.Add(ref elementRef, i) * Unsafe.Add(ref sourceRef, i);
            }
        }

        protected static void ShiftDataLevel(ref JpegBlock8x8F source, ref JpegBlock8x8 destination, int levelShift)
        {
            ref float sourceRef = ref Unsafe.As<JpegBlock8x8F, float>(ref source);
            ref short destinationRef = ref Unsafe.As<JpegBlock8x8, short>(ref destination);

            for (int i = 0; i < 64; i++)
            {
                Unsafe.Add(ref destinationRef, i) = (short)(JpegMathHelper.RoundToInt32(Unsafe.Add(ref sourceRef, i)) + levelShift);
            }
        }
    }
}
