using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using JpegLibrary.ColorConverters;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace JpegLibrary.Benchmarks
{
    [MemoryDiagnoser]
    public class EncoderBenchmark
    {
        private Rgba32[] _rgba;
        private int _width;
        private int _height;

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
                ctx.DrawImage(baseImage, new Point(0, 0), GraphicsOptions.Default);
                ctx.DrawImage(baseImage, new Point(0, baseImage.Height), GraphicsOptions.Default);
                ctx.DrawImage(baseImage, new Point(baseImage.Width, 0), GraphicsOptions.Default);
                ctx.DrawImage(baseImage, new Point(baseImage.Width, baseImage.Height), GraphicsOptions.Default);
            });
            ms.Seek(0, SeekOrigin.Begin);
            ms.SetLength(0);
            image.SaveAsJpeg(ms);

            byte[] inputBytes = ms.ToArray();

            var decoder = new JpegDecoder();
            decoder.SetInput(inputBytes);
            decoder.Identify();
            _width = decoder.Width;
            _height = decoder.Height;
            byte[] ycbcr = new byte[3 * _width * _height];
            decoder.SetOutputWriter(new JpegBufferOutputWriter(_width, _height, 3, ycbcr));
            decoder.Decode();

            _rgba = new Rgba32[_width * _height];
            JpegYCbCrToRgbConverter.Shared.ConvertYCbCr8ToRgba32(ycbcr, MemoryMarshal.AsBytes(_rgba.AsSpan()), _width * _height);
        }

        [Benchmark]
        public void TestImageSharpEncode444()
        {
            using var image = Image.WrapMemory<Rgba32>(_rgba, _width, _height);
            var stream = new NullWriteStream();
            image.SaveAsJpeg(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 75, Subsample = SixLabors.ImageSharp.Formats.Jpeg.JpegSubsample.Ratio444 });
        }

        [Benchmark]
        public void TestImageSharpEncode420()
        {
            using var image = Image.WrapMemory<Rgba32>(_rgba, _width, _height);
            var stream = new NullWriteStream();
            image.SaveAsJpeg(stream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 75, Subsample = SixLabors.ImageSharp.Formats.Jpeg.JpegSubsample.Ratio420 });
        }

        [Benchmark]
        public void TestJpegLibraryEncode444()
        {
            var encoder = new JpegEncoder();
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetLuminanceTable(JpegElementPrecision.Precision8Bit, 0), 75));
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetChrominanceTable(JpegElementPrecision.Precision8Bit, 1), 75));
            encoder.SetHuffmanTable(true, 0, JpegStandardHuffmanEncodingTable.GetLuminanceDCTable());
            encoder.SetHuffmanTable(false, 0, JpegStandardHuffmanEncodingTable.GetLuminanceACTable());
            encoder.SetHuffmanTable(true, 1, JpegStandardHuffmanEncodingTable.GetChrominanceDCTable());
            encoder.SetHuffmanTable(false, 1, JpegStandardHuffmanEncodingTable.GetChrominanceACTable());
            encoder.AddComponent(0, 0, 0, 1, 1); // Y component
            encoder.AddComponent(1, 1, 1, 1, 1); // Cb component
            encoder.AddComponent(1, 1, 1, 1, 1); // Cr component

            byte[] ycbcr = ArrayPool<byte>.Shared.Rent(3 * _width * _height);
            try
            {
                JpegRgbToYCbCrConverter.Shared.ConvertRgba32ToYCbCr8(MemoryMarshal.AsBytes(_rgba.AsSpan()), ycbcr, _width * _height);
                encoder.SetInputReader(new JpegBufferInputReader(_width, _height, 3, ycbcr));

                using var bufferWriter = new NullBufferWriter();
                encoder.SetOutput(bufferWriter);

                encoder.Encode();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(ycbcr);
            }
        }

        [Benchmark]
        public void TestJpegLibraryEncode420()
        {
            var encoder = new JpegEncoder();
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetLuminanceTable(JpegElementPrecision.Precision8Bit, 0), 75));
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetChrominanceTable(JpegElementPrecision.Precision8Bit, 1), 75));
            encoder.SetHuffmanTable(true, 0, JpegStandardHuffmanEncodingTable.GetLuminanceDCTable());
            encoder.SetHuffmanTable(false, 0, JpegStandardHuffmanEncodingTable.GetLuminanceACTable());
            encoder.SetHuffmanTable(true, 1, JpegStandardHuffmanEncodingTable.GetChrominanceDCTable());
            encoder.SetHuffmanTable(false, 1, JpegStandardHuffmanEncodingTable.GetChrominanceACTable());
            encoder.AddComponent(0, 0, 0, 1, 1); // Y component
            encoder.AddComponent(1, 1, 1, 2, 2); // Cb component
            encoder.AddComponent(1, 1, 1, 2, 2); // Cr component

            byte[] ycbcr = ArrayPool<byte>.Shared.Rent(3 * _width * _height);
            try
            {
                JpegRgbToYCbCrConverter.Shared.ConvertRgba32ToYCbCr8(MemoryMarshal.AsBytes(_rgba.AsSpan()), ycbcr, _width * _height);
                encoder.SetInputReader(new JpegBufferInputReader(_width, _height, 3, ycbcr));

                using var bufferWriter = new NullBufferWriter();
                encoder.SetOutput(bufferWriter);

                encoder.Encode();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(ycbcr);
            }
        }

        [Benchmark]
        public void TestJpegLibraryEncode444_NoBuffer()
        {
            var encoder = new JpegEncoder();
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetLuminanceTable(JpegElementPrecision.Precision8Bit, 0), 75));
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetChrominanceTable(JpegElementPrecision.Precision8Bit, 1), 75));
            encoder.SetHuffmanTable(true, 0, JpegStandardHuffmanEncodingTable.GetLuminanceDCTable());
            encoder.SetHuffmanTable(false, 0, JpegStandardHuffmanEncodingTable.GetLuminanceACTable());
            encoder.SetHuffmanTable(true, 1, JpegStandardHuffmanEncodingTable.GetChrominanceDCTable());
            encoder.SetHuffmanTable(false, 1, JpegStandardHuffmanEncodingTable.GetChrominanceACTable());
            encoder.AddComponent(0, 0, 0, 1, 1); // Y component
            encoder.AddComponent(1, 1, 1, 1, 1); // Cb component
            encoder.AddComponent(1, 1, 1, 1, 1); // Cr component

            encoder.SetInputReader(new JpegRgbaInputReader(_width, _height, _rgba));

            using var bufferWriter = new NullBufferWriter();
            encoder.SetOutput(bufferWriter);

            encoder.Encode();
        }

        [Benchmark]
        public void TestJpegLibraryEncode420_NoBuffer()
        {
            var encoder = new JpegEncoder();
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetLuminanceTable(JpegElementPrecision.Precision8Bit, 0), 75));
            encoder.SetQuantizationTable(JpegStandardQuantizationTable.ScaleByQuality(JpegStandardQuantizationTable.GetChrominanceTable(JpegElementPrecision.Precision8Bit, 1), 75));
            encoder.SetHuffmanTable(true, 0, JpegStandardHuffmanEncodingTable.GetLuminanceDCTable());
            encoder.SetHuffmanTable(false, 0, JpegStandardHuffmanEncodingTable.GetLuminanceACTable());
            encoder.SetHuffmanTable(true, 1, JpegStandardHuffmanEncodingTable.GetChrominanceDCTable());
            encoder.SetHuffmanTable(false, 1, JpegStandardHuffmanEncodingTable.GetChrominanceACTable());
            encoder.AddComponent(0, 0, 0, 1, 1); // Y component
            encoder.AddComponent(1, 1, 1, 2, 2); // Cb component
            encoder.AddComponent(1, 1, 1, 2, 2); // Cr component

            encoder.SetInputReader(new JpegRgbaInputReader(_width, _height, _rgba));

            using var bufferWriter = new NullBufferWriter();
            encoder.SetOutput(bufferWriter);

            encoder.Encode();
        }
    }
}
