using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace JpegLibrary.Tests.Optimizer
{
    public class OptimizerTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            string currentDir = Directory.GetCurrentDirectory();

            foreach (bool strip in new bool[] { true, false })
            {
                yield return new object[] {
                    Path.Join(currentDir, @"Assets/baseline/lake.jpg"),
                    strip
                };
            }
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void TestOptimize(string path, bool strip)
        {
            byte[] jpegBytes = File.ReadAllBytes(path);

            using var refImage = Image.Load<Rgb24>(path);

            var optimizer = new JpegOptimizer();
            optimizer.SetInput(jpegBytes);
            optimizer.Scan();

            var buffer = new ArrayBufferWriter<byte>();
            optimizer.SetOutput(buffer);
            optimizer.Optimize(strip);

            Assert.True(buffer.WrittenCount < jpegBytes.Length);

            using var testImage = Image.Load<Rgb24>(buffer.WrittenSpan);

            AssertEqual(refImage, testImage);
        }

        private static void AssertEqual<T>(Image<T> image1, Image<T> image2) where T : unmanaged, IPixel<T>
        {
            Assert.Equal(image1.Width, image2.Width);
            Assert.Equal(image1.Height, image2.Height);

            int height = image1.Height;
            for (int i = 0; i < height; i++)
            {
                Assert.True(image1.GetPixelRowSpan(i).SequenceEqual(image2.GetPixelRowSpan(i)));
            }
        }
    }
}
