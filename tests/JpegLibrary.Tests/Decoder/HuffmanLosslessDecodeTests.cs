using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace JpegLibrary.Tests.Decoder
{
    public class HuffmanLosslessDecodeTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            string currentDir = Directory.GetCurrentDirectory();
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless1_s22.jpg")
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless2_s22.jpg"),
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless3_s22.jpg"),
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless4_s22.jpg"),
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless5_s22.jpg"),
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless6_s22.jpg"),
            };
            yield return new object[] {
                Path.Join(currentDir, @"Assets/huffman_lossless/lossless7_s22.jpg"),
            };
        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void TestDecode(string path)
        {
            byte[] jpegBytes = File.ReadAllBytes(path);

            var decoder = new JpegDecoder();
            decoder.SetInput(jpegBytes);
            decoder.Identify();

            ushort[] buffer = new ushort[decoder.Width * decoder.Height * 4];
            var outputWriter = new JpegExtendingOutputWriter(decoder.Width, decoder.Height, 4, decoder.Precision, buffer);

            decoder.SetOutputWriter(outputWriter);
            decoder.Decode();

            ushort[] reference = ImageHelper.LoadBuffer(path, decoder.Width, decoder.Height, decoder.NumberOfComponents);

            Assert.True(reference.AsSpan().SequenceEqual(buffer));
        }
    }
}
