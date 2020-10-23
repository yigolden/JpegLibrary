using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace JpegLibrary.Benchmarks
{
    internal sealed class JpegRgbaInputReader : JpegBlockInputReader
    {
        private readonly int _width;
        private readonly int _height;
        private readonly Memory<Rgba32> _buffer;

        public JpegRgbaInputReader(int width, int height, Memory<Rgba32> buffer)
        {
            _width = width;
            _height = height;
            _buffer = buffer;
        }

        public override int Width => _width;

        public override int Height => _height;

        public override void ReadBlock(ref short blockRef, int componentIndex, int x, int y)
        {
            int width = _width;

            Span<byte> sourceSpan = MemoryMarshal.AsBytes(_buffer.Span);
            Span<short> componentSpan = stackalloc short[8];

            int blockWidth = Math.Min(width - x, 8);
            int blockHeight = Math.Min(_height - y, 8);

            if (blockWidth != 8 || blockHeight != 8)
            {
                Unsafe.As<short, JpegBlock8x8>(ref blockRef) = default;
            }

            switch (componentIndex)
            {
                case 0:
                    for (int offsetY = 0; offsetY < blockHeight; offsetY++)
                    {
                        int sourceRowOffset = (y + offsetY) * width + x;
                        ref short destinationRowRef = ref Unsafe.Add(ref blockRef, offsetY * 8);

                        JpegRgbToYCbCrComponentConverter.Shared.ConvertRgba32ToYComponent(sourceSpan.Slice(4 * sourceRowOffset), ref destinationRowRef, blockWidth);
                    }
                    break;
                case 1:
                    for (int offsetY = 0; offsetY < blockHeight; offsetY++)
                    {
                        int sourceRowOffset = (y + offsetY) * width + x;
                        ref short destinationRowRef = ref Unsafe.Add(ref blockRef, offsetY * 8);

                        JpegRgbToYCbCrComponentConverter.Shared.ConvertRgba32ToCbComponent(sourceSpan.Slice(4 * sourceRowOffset), ref destinationRowRef, blockWidth);
                    }
                    break;
                case 2:
                    for (int offsetY = 0; offsetY < blockHeight; offsetY++)
                    {
                        int sourceRowOffset = (y + offsetY) * width + x;
                        ref short destinationRowRef = ref Unsafe.Add(ref blockRef, offsetY * 8);

                        JpegRgbToYCbCrComponentConverter.Shared.ConvertRgba32ToCrComponent(sourceSpan.Slice(4 * sourceRowOffset), ref destinationRowRef, blockWidth);
                    }
                    break;
            }
        }
    }
}
