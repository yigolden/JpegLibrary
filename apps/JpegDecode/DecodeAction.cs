using JpegLibrary;
using JpegLibrary.ColorConverters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JpegDecode
{
    public class DecodeAction
    {

        public static Task<int> Decode(FileInfo source, string output)
        {
            using var writer = new MemoryPoolBufferWriter();

            using (FileStream stream = source.OpenRead())
            {
                ReadAllBytes(stream, writer);
            }

            var decoder = new JpegDecoder();
            decoder.SetInput(writer.GetReadOnlySequence());
            decoder.Identify();

            if (decoder.NumberOfComponents != 1 && decoder.NumberOfComponents != 3)
            {
                // We only support Grayscale and YCbCr.
                throw new NotSupportedException("This color space is not supported");
            }

            int width = decoder.Width;
            int height = decoder.Height;

            byte[] ycbcr = new byte[width * height * 3];

            if (decoder.Precision == 8)
            {
                // This is the most common case for JPEG.
                // We use the fatest implement.
                decoder.SetOutputWriter(new JpegBufferOutputWriter8Bit(width, height, 3, ycbcr));
            }
            else if (decoder.Precision < 8)
            {
                decoder.SetOutputWriter(new JpegBufferOutputWriterLessThan8Bit(width, height, decoder.Precision, 3, ycbcr));
            }
            else
            {
                decoder.SetOutputWriter(new JpegBufferOutputWriterGreaterThan8Bit(width, height, decoder.Precision, 3, ycbcr));
            }

            decoder.Decode();

            if (decoder.NumberOfComponents == 1)
            {
                // For grayscale image, we need to fill Cb and Cr in the YCbCr buffer.
                for (int i = 0; i < ycbcr.Length; i += 3)
                {
                    ycbcr[i + 1] = 128;
                    ycbcr[i + 2] = 128;
                }
            }

            using var image = new Image<Rgb24>(width, height);

            // Convert YCbCr to RGB
            for (int i = 0; i < height; i++)
            {
                JpegYCbCrToRgbConverter.Shared.ConvertYCbCr8ToRgb24(ycbcr.AsSpan(i * width * 3, width * 3), MemoryMarshal.AsBytes(image.GetPixelRowSpan(i)), width);
            }

            image.Save(output);

            return Task.FromResult(0);
        }

        const int BufferSize = 16384;

        private static void ReadAllBytes(Stream stream, IBufferWriter<byte> writer)
        {
            long length = stream.Length;
            while (length > 0)
            {
                int readSize = (int)Math.Min(length, BufferSize);
                Span<byte> buffer = writer.GetSpan(readSize);
                readSize = stream.Read(buffer);
                if (readSize == 0)
                {
                    break;
                }
                writer.Advance(readSize);
                length -= readSize;
            }
        }
    }
}
