using JpegLibrary;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegDecode
{
    public class JpegBufferOutputWriterGreaterThan8Bit : JpegBlockOutputWriter
    {
        private int _width;
        private int _height;
        private int _shift;
        private int _componentCount;
        private Memory<byte> _output;

        public JpegBufferOutputWriterGreaterThan8Bit(int width, int height, int precision, int componentCount, Memory<byte> output)
        {
            if (output.Length < (width * height * componentCount))
            {
                throw new ArgumentException("Destination buffer is too small.");
            }
            if (precision < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(precision));
            }

            _width = width;
            _height = height;
            _shift = precision - 8;
            _componentCount = componentCount;
            _output = output;
        }

        public override void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int componentCount = _componentCount;
            int width = _width;
            int height = _height;
            int shift = _shift;

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
                    Unsafe.Add(ref destinationRowRef, destX * componentCount) = ClampTo8Bit(Unsafe.Add(ref blockRef, destX) >> shift);
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampTo8Bit(int input)
        {
            return (byte)Math.Clamp(input, 0, 255);
        }
    }
}
