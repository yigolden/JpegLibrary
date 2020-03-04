using System.Collections.Generic;
using System.IO;
using Xunit;

namespace JpegLibrary.Tests.Decoder
{
    public class MetadataIdentifyTests
    {
        public class Metadata
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int NumberOfComponents { get; set; }
            public int Precision { get; set; }
            public int Quality { get; set; }
            public int JpegStreamSize { get; set; }
        }

        public static IEnumerable<object[]> GetIdentifyTestData()
        {
            string currentDir = Directory.GetCurrentDirectory();
            yield return new object[] {
                Path.Join(currentDir, @"Assets\baseline\cramps.jpg"),
                new Metadata
                {
                    Width = 800,
                    Height = 607,
                    NumberOfComponents = 1,
                    Precision = 8,
                    Quality = 90,
                    JpegStreamSize = 137_766
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\baseline\HETissueSlide.jpg"),
                new Metadata
                {
                    Width = 2048,
                    Height = 2048,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 75,
                    JpegStreamSize = 783_426
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\huffman_sequential\testorig12.jpg"),
                new Metadata
                {
                    Width = 227,
                    Height = 149,
                    NumberOfComponents = 3,
                    Precision = 12,
                    Quality = 75,
                    JpegStreamSize = 12_394
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\huffman_progressive\yellowcat_progressive_restart.jpg"),
                new Metadata
                {
                    Width = 720,
                    Height = 540,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 75,
                    JpegStreamSize = 45_703
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\huffman_progressive\progress.jpg"),
                new Metadata
                {
                    Width = 341,
                    Height = 486,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 85,
                    JpegStreamSize = 44_884
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\huffman_lossless\lossless1_s22.jpg"),
                new Metadata
                {
                    Width = 128,
                    Height = 128,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 0,
                    JpegStreamSize = 15_344
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\arithmetic_sequential\yellowcat_arith_restart.jpg"),
                new Metadata
                {
                    Width = 720,
                    Height = 540,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 75,
                    JpegStreamSize = 42_694
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\arithmetic_progressive\yellowcat_progressive_arith.jpg"),
                new Metadata
                {
                    Width = 720,
                    Height = 540,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 75,
                    JpegStreamSize = 42_260
                }
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets\arithmetic_progressive\yellowcat_progressive_arith_restart.jpg"),
                new Metadata
                {
                    Width = 720,
                    Height = 540,
                    NumberOfComponents = 3,
                    Precision = 8,
                    Quality = 75,
                    JpegStreamSize = 42_526
                }
            };
        }

        [Theory]
        [MemberData(nameof(GetIdentifyTestData))]
        public void TestDecoderIdentify(string path, Metadata data)
        {
            byte[] file = File.ReadAllBytes(path);
            var decoder = new JpegDecoder();
            decoder.SetInput(file);
            int size = decoder.Identify(loadQuantizationTables: true);

            Assert.Equal(data.Width, decoder.Width);
            Assert.Equal(data.Height, decoder.Height);
            Assert.Equal(data.NumberOfComponents, decoder.NumberOfComponents);
            Assert.Equal(data.Precision, decoder.Precision);
            if (data.Quality > 0)
            {
                Assert.True(decoder.TryEstimateQuanlity(out float quality));
                Assert.Equal(data.Quality, quality, 0);
            }
            if (data.JpegStreamSize > 0)
            {
                Assert.Equal(data.JpegStreamSize, size);
            }
        }
    }
}
