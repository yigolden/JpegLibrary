using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary.Tests
{
    public class JpegExtendingOutputWriter : JpegBlockOutputWriter
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _componentCount;
        private readonly int _precision;
        private readonly Memory<ushort> _output;

        public JpegExtendingOutputWriter(int width, int height, int componentCount, int precision, Memory<ushort> output)
        {
            if (output.Length < (width * height * componentCount))
            {
                throw new ArgumentException("Destination buffer is too small.");
            }

            _width = width;
            _height = height;
            _componentCount = componentCount;
            _precision = precision;
            _output = output;
        }

        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int precision = _precision;
            ushort max = (ushort)((1 << precision) - 1);
            int componentCount = _componentCount;
            int width = _width;
            int height = _height;

            if (x > width || y > _height)
            {
                return;
            }

            int writeWidth = Math.Min(width - x, 8);
            int writeHeight = Math.Min(height - y, 8);

            ref ushort destinationRef = ref MemoryMarshal.GetReference(_output.Span);
            destinationRef = ref Unsafe.Add(ref destinationRef, y * width * componentCount + x * componentCount + componentIndex);

            // Fast path
            if (precision >= 8)
            {
                for (int destY = 0; destY < writeHeight; destY++)
                {
                    ref ushort destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * componentCount);
                    for (int destX = 0; destX < writeWidth; destX++)
                    {
                        uint value = Clamp((ushort)Unsafe.Add(ref blockRef, destX), max);
                        Unsafe.Add(ref destinationRowRef, destX * componentCount) = (ushort)FastExpandBits(value, precision);
                    }
                    blockRef = ref Unsafe.Add(ref blockRef, 8);
                }
                return;
            }

            for (int destY = 0; destY < writeHeight; destY++)
            {
                ref ushort destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * componentCount);
                for (int destX = 0; destX < writeWidth; destX++)
                {
                    uint value = Clamp((ushort)Unsafe.Add(ref blockRef, destX), max);
                    Unsafe.Add(ref destinationRowRef, destX * componentCount) = (ushort)ExpandBits(value, precision);
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }
        }

        private ushort Clamp(ushort value, ushort max)
        {
            return Math.Clamp(value, (ushort)0, max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint FastExpandBits(uint bits, int bitCount)
        {
            const int TargetBitCount = 16;
            Debug.Assert(bitCount * 2 >= TargetBitCount);
            int remainingBits = TargetBitCount - bitCount;
            return (bits << remainingBits) | (bits & ((uint)(1 << remainingBits) - 1));
        }

        private static uint ExpandBits(uint bits, int bitCount)
        {
            const int TargetBitCount = 16;

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
