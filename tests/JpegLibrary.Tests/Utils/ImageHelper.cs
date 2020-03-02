using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace JpegLibrary.Tests
{
    internal static class ImageHelper
    {
        public static ushort[] LoadBuffer(string pathBase, int width, int height, int numberOfComponents)
        {
            string pathHigh = pathBase + ".high.png";
            string pathLowDiff = pathBase + ".low-diff.png";

            using var high = Image.Load<Rgba32>(pathHigh);
            using var lowDiff = Image.Load<Rgba32>(pathLowDiff);

            if (high.Width != width || high.Height != height || lowDiff.Width != width || lowDiff.Height != height)
            {
                throw new InvalidDataException();
            }

            Rgba32[] highPixels = new Rgba32[width * height];
            Rgba32[] lowDiffPixels = new Rgba32[width * height];

            for (int i = 0; i < height; i++)
            {
                high.GetPixelRowSpan(i).CopyTo(highPixels.AsSpan(width * i, width));
                lowDiff.GetPixelRowSpan(i).CopyTo(lowDiffPixels.AsSpan(width * i, width));
            }

            ushort[] buffer = new ushort[width * height * 4];

            CopyHighBits(highPixels, buffer, numberOfComponents);

            CopyLowBits(lowDiffPixels, buffer, numberOfComponents);

            ReversePrediction(buffer, numberOfComponents);

            return buffer;
        }

        private static void CopyHighBits(Rgba32[] pixels, ushort[] buffer, int numberOfComponents)
        {
            ref byte pixelRef = ref Unsafe.As<Rgba32, byte>(ref pixels[0]);
            ref ushort bufferRef = ref buffer[0];

            for (int i = 0; i < pixels.Length; i++)
            {
                for (int n = 0; n < numberOfComponents; n++)
                {
                    Unsafe.Add(ref bufferRef, n) = (ushort)(Unsafe.Add(ref pixelRef, n) << 8);
                }
                bufferRef = ref Unsafe.Add(ref bufferRef, 4);
                pixelRef = ref Unsafe.Add(ref pixelRef, 4);
            }
        }

        private static void CopyLowBits(Rgba32[] pixels, ushort[] buffer, int numberOfComponents)
        {
            ref byte pixelRef = ref Unsafe.As<Rgba32, byte>(ref pixels[0]);
            ref ushort bufferRef = ref buffer[0];

            for (int i = 0; i < pixels.Length; i++)
            {
                for (int n = 0; n < numberOfComponents; n++)
                {
                    Unsafe.Add(ref bufferRef, n) = (ushort)(Unsafe.Add(ref bufferRef, n) | Unsafe.Add(ref pixelRef, n));
                }
                bufferRef = ref Unsafe.Add(ref bufferRef, 4);
                pixelRef = ref Unsafe.Add(ref pixelRef, 4);
            }
        }

        private static void ReversePrediction(ushort[] buffer, int numberOfComponents)
        {
            int pixelCount = buffer.Length / 4;
            for (int i = 0; i < pixelCount; i++)
            {
                for (int n = 0; n < numberOfComponents; n++)
                {
                    ref ushort bufferRef = ref buffer[i * 4 + n];
                    int high = bufferRef & 0xff00;
                    int low = (byte)bufferRef;
                    low = (high >> 8) ^ low;
                    bufferRef = (ushort)(high | low);
                }
            }
        }
    }
}
