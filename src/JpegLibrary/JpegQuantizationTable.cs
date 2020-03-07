#nullable enable

using System;
using System.Buffers;

namespace JpegLibrary
{
    /// <summary>
    /// JPEG quantization table.
    /// </summary>
    public readonly struct JpegQuantizationTable
    {
        private readonly ushort[] _elements;

        /// <summary>
        /// Initialize a quantization table object.
        /// </summary>
        /// <param name="elementPrecision">The element precision. 0 for 8 bit precision. 1 for 12 bit precision.</param>
        /// <param name="identifier">The identifier of the quantization table.</param>
        /// <param name="elements">The elements of the quantization table in zig-zag order.</param>
        public JpegQuantizationTable(byte elementPrecision, byte identifier, ushort[] elements)
        {
            ElementPrecision = elementPrecision;
            Identifier = identifier;
            _elements = elements ?? throw new ArgumentNullException(nameof(elements));

            if (elements.Length != 64)
            {
                throw new ArgumentException("The length of elements must be 64.");
            }
        }

        /// <summary>
        /// The element precision. 0 for 8 bit precision. 1 for 12 bit precision.
        /// </summary>
        public byte ElementPrecision { get; }

        /// <summary>
        /// The identifier of the quantization table.
        /// </summary>
        public byte Identifier { get; }

        /// <summary>
        /// Gets the elements of the quantization table in zig-zag order.
        /// </summary>
        public ReadOnlySpan<ushort> Elements => _elements;

        /// <summary>
        /// Gets whether this quantization is empty (not initialized).
        /// </summary>
        public bool IsEmpty => ElementPrecision == 0 && Identifier == 0 && _elements is null;

        /// <summary>
        /// Get the byte count required when writing this quantization table into JPEG stream.
        /// </summary>
        public byte BytesRequired => ElementPrecision == 0 ? (byte)(64 + 1) : (byte)(128 + 1);

        /// <summary>
        /// Parse the quantization table from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="quantizationTable">The quantization table parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the scan component is successfully parsed.</returns>
        public static bool TryParse(ReadOnlySequence<byte> buffer, out JpegQuantizationTable quantizationTable, out int bytesConsumed)
        {
            if (buffer.IsSingleSegment)
            {
#if NO_READONLYSEQUENCE_FISTSPAN
                return TryParse(buffer.First.Span, out quantizationTable, out bytesConsumed);
#else
                return TryParse(buffer.FirstSpan, out quantizationTable, out bytesConsumed);
#endif
            }

            bytesConsumed = 0;
            if (buffer.IsEmpty)
            {
                quantizationTable = default;
                return false;
            }

#if NO_READONLYSEQUENCE_FISTSPAN
            byte b = buffer.First.Span[0];
#else
            byte b = buffer.FirstSpan[0];
#endif
            bytesConsumed++;

            return TryParse((byte)(b >> 4), (byte)(b & 0xf), buffer.Slice(1), out quantizationTable, ref bytesConsumed);
        }

        /// <summary>
        /// Parse the quantization table from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="quantizationTable">The quantization table parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the scan component is successfully parsed.</returns>
        public static bool TryParse(ReadOnlySpan<byte> buffer, out JpegQuantizationTable quantizationTable, out int bytesConsumed)
        {
            bytesConsumed = 0;
            if (buffer.IsEmpty)
            {
                quantizationTable = default;
                return false;
            }

            byte b = buffer[0];
            bytesConsumed++;

            return TryParse((byte)(b >> 4), (byte)(b & 0xf), buffer.Slice(1), out quantizationTable, ref bytesConsumed);
        }

        /// <summary>
        /// Parse the quantization table from the buffer.
        /// </summary>
        /// <param name="precision">The precision of the quantization table.</param>
        /// <param name="identifier">The identifier of the quantization table.</param>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="quantizationTable">The quantization table parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the scan component is successfully parsed.</returns>
        public static bool TryParse(byte precision, byte identifier, ReadOnlySequence<byte> buffer, out JpegQuantizationTable quantizationTable, ref int bytesConsumed)
        {
            if (buffer.IsSingleSegment)
            {
#if NO_READONLYSEQUENCE_FISTSPAN
                return TryParse(precision, identifier, buffer.First.Span, out quantizationTable, ref bytesConsumed);
#else
                return TryParse(precision, identifier, buffer.FirstSpan, out quantizationTable, ref bytesConsumed);
#endif
            }

            ushort[] elements;
            Span<byte> local = stackalloc byte[128];
            if (precision == 0)
            {
                if (buffer.Length < 64)
                {
                    quantizationTable = default;
                    return false;
                }

                buffer.Slice(0, 64).CopyTo(local);

                elements = new ushort[64];
                for (int i = 0; i < 64; i++)
                {
                    elements[i] = local[i];
                }
                bytesConsumed += 64;
            }
            else if (precision == 1)
            {
                if (buffer.Length < 128)
                {
                    quantizationTable = default;
                    return false;
                }

                buffer.Slice(0, 128).CopyTo(local);

                elements = new ushort[64];
                for (int i = 0; i < 64; i++)
                {
                    elements[i] = (ushort)(local[2 * i] << 8 | local[2 * i + 1]);
                }
                bytesConsumed += 128;
            }
            else
            {
                quantizationTable = default;
                return false;
            }

            quantizationTable = new JpegQuantizationTable(precision, identifier, elements);
            return true;
        }

        /// <summary>
        /// Parse the quantization table from the buffer.
        /// </summary>
        /// <param name="precision">The precision of the quantization table.</param>
        /// <param name="identifier">The identifier of the quantization table.</param>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="quantizationTable">The quantization table parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the scan component is successfully parsed.</returns>
        public static bool TryParse(byte precision, byte identifier, ReadOnlySpan<byte> buffer, out JpegQuantizationTable quantizationTable, ref int bytesConsumed)
        {
            ushort[] elements;
            if (precision == 0)
            {
                if (buffer.Length < 64)
                {
                    quantizationTable = default;
                    return false;
                }

                elements = new ushort[64];
                for (int i = 0; i < 64; i++)
                {
                    elements[i] = buffer[i];
                }
                bytesConsumed += 64;
            }
            else if (precision == 1)
            {
                if (buffer.Length < 128)
                {
                    quantizationTable = default;
                    return false;
                }

                elements = new ushort[64];
                for (int i = 0; i < 64; i++)
                {
                    elements[i] = (ushort)(buffer[2 * i] << 8 | buffer[2 * i + 1]);
                }
                bytesConsumed += 128;
            }
            else
            {
                quantizationTable = default;
                return false;
            }

            quantizationTable = new JpegQuantizationTable(precision, identifier, elements);
            return true;
        }

        /// <summary>
        /// Write the quantization table into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesWritten">The count of bytes written.</param>
        /// <returns>True if the destination buffer is large enough.</returns>
        public bool TryWrite(Span<byte> buffer, out int bytesWritten)
        {
            bytesWritten = 0;
            if (buffer.IsEmpty)
            {
                return false;
            }

            buffer[0] = (byte)(ElementPrecision << 4 | (Identifier & 0xf));
            buffer = buffer.Slice(1);
            bytesWritten++;

            ReadOnlySpan<ushort> elements = Elements;
            if (ElementPrecision == 0)
            {
                if (buffer.Length < 64)
                {
                    return false;
                }

                for (int i = 0; i < 64; i++)
                {
                    buffer[i] = (byte)elements[i];
                }
                bytesWritten += 64;
            }
            else if (ElementPrecision == 1)
            {
                if (buffer.Length < 128)
                {
                    return false;
                }

                for (int i = 0; i < 64; i++)
                {
                    buffer[2 * i] = (byte)(elements[i] >> 8);
                    buffer[2 * i + 1] = (byte)elements[i];
                }
                bytesWritten += 128;
            }

            return true;
        }
    }
}
