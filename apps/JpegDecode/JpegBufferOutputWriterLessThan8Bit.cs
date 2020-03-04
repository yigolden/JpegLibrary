using JpegLibrary;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegDecode
{
    public class JpegBufferOutputWriterLessThan8Bit : JpegBlockOutputWriter
    {
        private int _width;
        private int _height;
        private int _precision;
        private int _componentCount;
        private Memory<byte> _output;

        public JpegBufferOutputWriterLessThan8Bit(int width, int height, int precision, int componentCount, Memory<byte> output)
        {
            if (output.Length < (width * height * componentCount))
            {
                throw new ArgumentException("Destination buffer is too small.");
            }
            if (precision > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(precision));
            }

            _width = width;
            _height = height;
            _precision = precision;
            _componentCount = componentCount;
            _output = output;
        }

        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int componentCount = _componentCount;
            int width = _width;
            int height = _height;
            int precision = _precision;
            int max = (1 << precision) - 1;

            if (x > width || y > _height)
            {
                return;
            }

            int writeWidth = Math.Min(width - x, 8);
            int writeHeight = Math.Min(height - y, 8);

            ref byte destinationRef = ref MemoryMarshal.GetReference(_output.Span);
            destinationRef = ref Unsafe.Add(ref destinationRef, y * width * componentCount + x * componentCount + componentIndex);

            for (int destY = 0; destY < writeHeight; destY++)
            {
                ref byte destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * componentCount);
                for (int destX = 0; destX < writeWidth; destX++)
                {
                    int value = Math.Clamp(Unsafe.Add(ref blockRef, destX), 0, max);
                    Unsafe.Add(ref destinationRowRef, destX * componentCount) = (byte)ExpandBits((uint)value, precision);
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FastExpandBits(uint bits, int bitCount)
        {
            const int TargetBitCount = 8;
            Debug.Assert(bitCount * 2 >= TargetBitCount);
            int remainingBits = TargetBitCount - bitCount;
            return (bits << remainingBits) | (bits & ((uint)(1 << remainingBits) - 1));
        }

        private static uint ExpandBits(uint bits, int bitCount)
        {
            const int TargetBitCount = 8;
            int currentBitCount = bitCount;
            while (currentBitCount < TargetBitCount)
            {
                bits = (bits << bitCount) | bits;
                currentBitCount += bitCount;
            }

            if (currentBitCount > TargetBitCount)
            {
                bits = bits >> bitCount;
                currentBitCount -= bitCount;
                bits = FastExpandBits(bits, currentBitCount);
            }

            return bits;
        }
    }
}
