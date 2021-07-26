﻿#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary
{
    /// <summary>
    /// Helper class for acquiring standard JPEG huffman encoding table.
    /// </summary>
    public class JpegStandardHuffmanEncodingTable
    {
        private static ReadOnlySpan<byte> LuminanceDCCodeLengths => new byte[] { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
        private static ReadOnlySpan<byte> LuminanceDCCodeValues => new byte[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
        };

        private static ReadOnlySpan<byte> LuminanceACCodeLengths => new byte[] { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 125 };
        private static ReadOnlySpan<byte> LuminanceACCodeValues => new byte[]
        {
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11,
            0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13,
            0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32,
            0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1,
            0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33,
            0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17,
            0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38,
            0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47,
            0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56,
            0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65,
            0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74,
            0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x83,
            0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a,
            0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
            0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6,
            0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
            0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2,
            0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9,
            0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7,
            0xd8, 0xd9, 0xda, 0xe1, 0xe2, 0xe3, 0xe4,
            0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1,
            0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
            0xf9, 0xfa
        };

        private static ReadOnlySpan<byte> ChrominanceDCCodeLengths => new byte[] { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
        private static ReadOnlySpan<byte> ChrominanceDCCodeValues => new byte[]
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
        };

        private static ReadOnlySpan<byte> ChrominanceACCodeLengths => new byte[] { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 119 };
        private static ReadOnlySpan<byte> ChrominanceACCodeValues => new byte[]
        {
            0x00, 0x01, 0x02, 0x03, 0x11, 0x04,
            0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51,
            0x07, 0x61, 0x71, 0x13, 0x22, 0x32, 0x81,
            0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1,
            0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62,
            0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34, 0xe1,
            0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
            0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37,
            0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46,
            0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55,
            0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64,
            0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73,
            0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
            0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88,
            0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
            0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4,
            0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2,
            0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9,
            0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7,
            0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5,
            0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe2, 0xe3,
            0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
            0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
            0xf9, 0xfa
        };

        [SkipLocalsInit]
        private static JpegHuffmanCanonicalCode[] BuildCanonicalCode(ReadOnlySpan<byte> codeLengths, ReadOnlySpan<byte> codeValues)
        {
            int codeCount = codeValues.Length;
            var codes = new JpegHuffmanCanonicalCode[codeCount];

            Span<byte> codeLengthsBuffer = stackalloc byte[16];
            codeLengths.CopyTo(codeLengthsBuffer);

            int currentCodeLength = 1;
            ref byte codeLengthsRef = ref MemoryMarshal.GetReference(codeLengthsBuffer);

            for (int i = 0; i < codes.Length; i++)
            {
                while (codeLengthsRef == 0)
                {
                    codeLengthsRef = ref Unsafe.Add(ref codeLengthsRef, 1);
                    currentCodeLength++;
                }
                codeLengthsRef--;

                codes[i].Symbol = codeValues[i];
                codes[i].CodeLength = (byte)currentCodeLength;
            }

            ushort bitCode = codes[0].Code = 0;
            int bitCount = codes[0].CodeLength;

            for (int i = 1; i < codes.Length; i++)
            {
                ref JpegHuffmanCanonicalCode code = ref codes[i];

                if (code.CodeLength > bitCount)
                {
                    bitCode++;
                    bitCode <<= (code.CodeLength - bitCount);
                    code.Code = bitCode;
                    bitCount = code.CodeLength;
                }
                else
                {
                    code.Code = ++bitCode;
                }
            }

            return codes;
        }


        private static JpegHuffmanEncodingTable? s_luminanceDCTable;
        private static JpegHuffmanEncodingTable? s_luminanceACTable;
        private static JpegHuffmanEncodingTable? s_chrominanceDCTable;
        private static JpegHuffmanEncodingTable? s_chrominanceACTable;

        /// <summary>
        /// Gets the standard Huffman encoding table for DC coefficient of luminance component.
        /// </summary>
        /// <returns>The Huffman encoding table for DC coefficient of luminance component.</returns>
        public static JpegHuffmanEncodingTable GetLuminanceDCTable()
        {
            JpegHuffmanEncodingTable? table = s_luminanceDCTable;
            if (table is null)
            {
                s_luminanceDCTable = table = new JpegHuffmanEncodingTable(BuildCanonicalCode(LuminanceDCCodeLengths, LuminanceDCCodeValues));
            }
            return table;
        }

        /// <summary>
        /// Gets the standard Huffman encoding table for RLE-encoded AC coefficient of chrominance component.
        /// </summary>
        /// <returns>The Huffman encoding table for RLE-encoded AC coefficient of chrominance component.</returns>
        public static JpegHuffmanEncodingTable GetLuminanceACTable()
        {
            JpegHuffmanEncodingTable? table = s_luminanceACTable;
            if (table is null)
            {
                s_luminanceACTable = table = new JpegHuffmanEncodingTable(BuildCanonicalCode(LuminanceACCodeLengths, LuminanceACCodeValues));
            }
            return table;
        }

        /// <summary>
        /// Gets the standard Huffman encoding table for DC coefficient of chrominance component.
        /// </summary>
        /// <returns>The Huffman encoding table for DC coefficient of chrominance component.</returns>
        public static JpegHuffmanEncodingTable GetChrominanceDCTable()
        {
            JpegHuffmanEncodingTable? table = s_chrominanceDCTable;
            if (table is null)
            {
                s_chrominanceDCTable = table = new JpegHuffmanEncodingTable(BuildCanonicalCode(ChrominanceDCCodeLengths, ChrominanceDCCodeValues));
            }
            return table;
        }

        /// <summary>
        /// Gets the standard Huffman encoding table for RLE-encoded AC coefficient of chrominance component.
        /// </summary>
        /// <returns>The Huffman encoding table for RLE-encoded AC coefficient of chrominance component.</returns>
        public static JpegHuffmanEncodingTable GetChrominanceACTable()
        {
            JpegHuffmanEncodingTable? table = s_chrominanceACTable;
            if (table is null)
            {
                s_chrominanceACTable = table = new JpegHuffmanEncodingTable(BuildCanonicalCode(ChrominanceACCodeLengths, ChrominanceACCodeValues));
            }
            return table;
        }

    }
}
