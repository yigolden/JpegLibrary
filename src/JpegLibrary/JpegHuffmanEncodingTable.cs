#nullable enable

using System;
using System.Diagnostics;

namespace JpegLibrary
{
    /// <summary>
    /// A Huffman encoding table to encode symbols into JPEG stream.
    /// </summary>
    public class JpegHuffmanEncodingTable
    {
        private readonly int _codeCount;
        private readonly JpegHuffmanCanonicalCode[] _codes;
        private readonly byte[] _symbolMap;

        /// <summary>
        /// Initialize the table with the specified canonical code.
        /// </summary>
        /// <param name="codes">The canonical code used to initialize the table.</param>
        public JpegHuffmanEncodingTable(JpegHuffmanCanonicalCode[] codes)
        {
            _codes = codes ?? throw new ArgumentNullException(nameof(codes));
            Array.Sort(codes, JpegHuffmanCanonicalCodeCompareByCodeLen.Instance);

            int codeCount = 0;
            _symbolMap = new byte[256];
            for (int i = 0; i < codes.Length; i++)
            {
                JpegHuffmanCanonicalCode code = codes[i];
                if (code.CodeLength != 0)
                {
                    _symbolMap[code.Symbol] = (byte)i;
                    codeCount++;
                }
            }
            _codeCount = codeCount;
        }

        /// <summary>
        /// the count of bytes required to encode this Huffman table.
        /// </summary>
        public ushort BytesRequired => (ushort)(16 + _codeCount);

        /// <summary>
        /// Write the Huffman table into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesWritten">The count of bytes written.</param>
        /// <returns>True if the destination buffer is large enough.</returns>
        public bool TryWrite(Span<byte> buffer, out int bytesWritten)
        {
            bytesWritten = 0;
            if (buffer.Length < 16)
            {
                return false;
            }

            for (int len = 1; len <= 16; len++)
            {
                int count = 0;
                for (int i = _codes.Length - _codeCount; i < _codes.Length; i++)
                {
                    if (_codes[i].CodeLength == len)
                    {
                        count++;
                    }
                }
                buffer[len - 1] = (byte)count;
            }
            buffer = buffer.Slice(16);
            bytesWritten += 16;

            if (buffer.Length < _codeCount)
            {
                return false;
            }

            int index = 0;
            for (int i = _codes.Length - _codeCount; i < _codes.Length; i++)
            {
                buffer[index++] = _codes[i].Symbol;
            }
            bytesWritten += index;

            return true;
        }

        /// <summary>
        /// Get the Huffman code for the specified symbol.
        /// </summary>
        /// <param name="symbol">The symbol to encode.</param>
        /// <param name="code">The Huffman code of the symbol.</param>
        /// <param name="codeLength">The length of the Huffman code.</param>
        public void GetCode(int symbol, out ushort code, out int codeLength)
        {
            Debug.Assert((uint)symbol < 256);
            JpegHuffmanCanonicalCode c = _codes[_symbolMap[symbol]];
            code = c.Code;
            codeLength = c.CodeLength;
        }
    }
}
