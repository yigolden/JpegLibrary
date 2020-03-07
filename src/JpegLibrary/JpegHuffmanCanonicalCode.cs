#nullable enable

using System;
using System.Collections.Generic;

namespace JpegLibrary
{
    /// <summary>
    /// Represents a Huffman canonical code.
    /// </summary>
    public struct JpegHuffmanCanonicalCode
    {
        /// <summary>
        /// The code value of the symbol.
        /// </summary>
        public ushort Code { get; set; }

        /// <summary>
        /// The actual symbol.
        /// </summary>
        public byte Symbol { get; set; }

        /// <summary>
        /// The length of the code.
        /// </summary>
        public byte CodeLength { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"JpegCanonicalCode(Symbol={Symbol},Code={Convert.ToString(Code, 2).PadLeft(CodeLength, '0')},CodeLength={CodeLength})";
        }
    }

    internal class JpegHuffmanCanonicalCodeCompareByCodeLen : Comparer<JpegHuffmanCanonicalCode>
    {
        public static JpegHuffmanCanonicalCodeCompareByCodeLen Instance { get; } = new JpegHuffmanCanonicalCodeCompareByCodeLen();

        public override int Compare(JpegHuffmanCanonicalCode x, JpegHuffmanCanonicalCode y)
        {
            if (x.CodeLength > y.CodeLength)
            {
                return 1;
            }
            else if (x.CodeLength < y.CodeLength)
            {
                return -1;
            }
            else
            {
                if (x.Symbol > y.Symbol)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
        }
    }
}
