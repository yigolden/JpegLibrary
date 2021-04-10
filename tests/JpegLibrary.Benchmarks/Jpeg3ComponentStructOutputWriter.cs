using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary.Benchmarks
{
    internal readonly struct Jpeg3ComponentStructOutputWriter : IJpegBlockOutputWriter
    {
        private readonly byte[] _output;
        private readonly int _width;
        private readonly int _height;

        public Jpeg3ComponentStructOutputWriter(byte[] output, int width, int height)
        {
            if (output.Length < (width * height * 3))
            {
                throw new ArgumentException("Destination buffer is too small.");
            }

            _width = width;
            _height = height;
            _output = output;
        }

        public void WriteBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int width = _width;
            int height = _height;

            if (x > width || y > _height)
            {
                return;
            }

            int writeWidth = Math.Min(width - x, 8);
            int writeHeight = Math.Min(height - y, 8);

            ref byte destinationRef = ref MemoryMarshal.GetReference(_output.AsSpan());
            destinationRef = ref Unsafe.Add(ref destinationRef, y * width * 3 + x * 3 + componentIndex);

            for (int destY = 0; destY < writeHeight; destY++)
            {
                ref byte destinationRowRef = ref Unsafe.Add(ref destinationRef, destY * width * 3);
                for (int destX = 0; destX < writeWidth; destX++)
                {
                    Unsafe.Add(ref destinationRowRef, destX * 3) = ClampTo8Bit(Unsafe.Add(ref blockRef, destX));
                }
                blockRef = ref Unsafe.Add(ref blockRef, 8);
            }
        }

        private static byte ClampTo8Bit(short input)
        {
#if NO_MATH_CLAMP
            return (byte)Math.Min(Math.Max(input, (short)0), (short)255);
#else
            return (byte)Math.Clamp(input, (short)0, (short)255);
#endif
        }
    }
}
