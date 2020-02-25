using System.Collections.Generic;
using System.IO;
using Xunit;

namespace JpegLibrary.Tests
{
    public class DecoderMetadataTests
    {
        public class Metadata
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int NumberOfComponents { get; set; }
            public int Precision { get; set; }
            public int Quality { get; set; }
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
                    Quality = 90
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
                    Quality = 75
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
            decoder.Identify(loadQuantizationTables: true);

            Assert.Equal(data.Width, decoder.Width);
            Assert.Equal(data.Height, decoder.Height);
            Assert.Equal(data.NumberOfComponents, decoder.NumberOfComponents);
            Assert.Equal(data.Precision, decoder.Precision);
            Assert.True(decoder.TryEstimateQuanlity(out float quality));
            Assert.Equal(data.Quality, quality, 0);
        }
    }
}
