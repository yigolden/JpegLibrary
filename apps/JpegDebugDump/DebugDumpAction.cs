using JpegLibrary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace JpegDebugDump
{
    internal class DebugDumpAction
    {
        public static Task<int> DebugDump(FileInfo source, string output, CancellationToken cancellationToken)
        {
            if (source is null || source.Length == 0)
            {
                Console.WriteLine("Input file are not specified.");
                return Task.FromResult(1);
            }
            if (output is null)
            {
                Console.WriteLine("Output file is not specified");
                return Task.FromResult(1);
            }

            byte[] input = File.ReadAllBytes(source.FullName);

            var decoder = new JpegDecoder();
            decoder.SetInput(input);
            decoder.Identify();

            int numberOfComponents = decoder.NumberOfComponents;
            if (numberOfComponents > 4)
            {
                throw new NotSupportedException("Number of components greater than 4 is not supported.");
            }

            ushort[] buffer = new ushort[decoder.Width * decoder.Height * 4];
            var outputWriter = new JpegExtendingOutputWriter(decoder.Width, decoder.Height, 4, decoder.Precision, buffer);

            decoder.SetOutputWriter(outputWriter);
            decoder.Decode();

            // We use RGBA PNG image to store 4 components.
            // Its content may be Grayscale, YCbCr or others.
            Rgba32[] pixels = new Rgba32[decoder.Width * decoder.Height];
            Array.Fill(pixels, Rgba32.White);
            using var image = Image.WrapMemory(pixels.AsMemory(), decoder.Width, decoder.Height);

            // high bits
            CopyHighBits(buffer, pixels, numberOfComponents);
            image.Save(output + ".high.png");

            // apply prediction
            ApplyPrediction(buffer);

            // low bits
            CopyLowBits(buffer, pixels, numberOfComponents);
            image.Save(output + ".low-diff.png");

            return Task.FromResult(0);
        }

        private static void CopyHighBits(ushort[] buffer, Rgba32[] pixels, int numberOfComponents)
        {
            ref ushort bufferRef = ref buffer[0];
            ref byte pixelRef = ref Unsafe.As<Rgba32, byte>(ref pixels[0]);

            for (int i = 0; i < pixels.Length; i++)
            {
                for (int n = 0; n < numberOfComponents; n++)
                {
                    Unsafe.Add(ref pixelRef, n) = (byte)(Unsafe.Add(ref bufferRef, n) >> 8);
                }
                bufferRef = ref Unsafe.Add(ref bufferRef, 4);
                pixelRef = ref Unsafe.Add(ref pixelRef, 4);
            }
        }

        private static void ApplyPrediction(ushort[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                int high = buffer[i] >> 8;
                int low = (byte)buffer[i];
                buffer[i] = (ushort)(high ^ low);
            }
        }

        private static void CopyLowBits(ushort[] buffer, Rgba32[] pixels, int numberOfComponents)
        {
            ref ushort bufferRef = ref buffer[0];
            ref byte pixelRef = ref Unsafe.As<Rgba32, byte>(ref pixels[0]);

            for (int i = 0; i < pixels.Length; i++)
            {
                for (int n = 0; n < numberOfComponents; n++)
                {
                    Unsafe.Add(ref pixelRef, n) = (byte)Unsafe.Add(ref bufferRef, n);
                }
                bufferRef = ref Unsafe.Add(ref bufferRef, 4);
                pixelRef = ref Unsafe.Add(ref pixelRef, 4);
            }
        }

    }
}
