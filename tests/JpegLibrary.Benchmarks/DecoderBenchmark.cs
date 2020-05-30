using BenchmarkDotNet.Attributes;
using JpegLibrary.ColorConverters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Buffers;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace JpegLibrary.Benchmarks
{
    [MemoryDiagnoser]
    public class DecoderBenchmark
    {
        private byte[] _inputBytes;

        [GlobalSetup]
        public void Setup()
        {
            var ms = new MemoryStream();
            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("JpegLibrary.Benchmarks.Resources.HETissueSlide.jpg"))
            {
                resourceStream.CopyTo(ms);
            }
            ms.Seek(0, SeekOrigin.Begin);

            // Load the image and expand it
            using var baseImage = Image.Load(ms);
            using var image = new Image<Rgba32>(baseImage.Width * 4, baseImage.Height * 4);
            image.Mutate(ctx =>
            {
                ctx.DrawImage(baseImage, new Point(0, 0), opacity: 1);
                ctx.DrawImage(baseImage, new Point(0, baseImage.Height), opacity: 1);
                ctx.DrawImage(baseImage, new Point(baseImage.Width, 0), opacity: 1);
                ctx.DrawImage(baseImage, new Point(baseImage.Width, baseImage.Height), opacity: 1);
            });
            ms.Seek(0, SeekOrigin.Begin);
            ms.SetLength(0);
            image.SaveAsJpeg(ms);
            _inputBytes = ms.ToArray();
        }

        [Benchmark(Baseline = true)]
        public void TestImageSharp()
        {
            using var image = Image.Load(_inputBytes);
        }

        [Benchmark]
        public void TestJpegLibrary()
        {
            var decoder = new JpegDecoder();
            decoder.SetInput(_inputBytes);
            decoder.Identify();
            int width = decoder.Width;
            int height = decoder.Height;
            Rgba32[] rgba = new Rgba32[width * height];
            byte[] ycbcr = ArrayPool<byte>.Shared.Rent(3 * rgba.Length);
            try
            {
                var outputWriter = new JpegBufferOutputWriter(decoder.Width, decoder.Height, 3, ycbcr);
                decoder.SetOutputWriter(outputWriter);
                decoder.Decode();

                JpegYCbCrToRgbConverter.Shared.ConvertYCbCr8ToRgba32(ycbcr, MemoryMarshal.AsBytes(rgba.AsSpan()), decoder.Width * decoder.Height);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(ycbcr);
            }
        }
    }
}
