#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JpegLibrary
{
    /// <summary>
    /// A mutable struct to read markers and other content from JPEG stream.
    /// </summary>
    public struct JpegReader
    {
        private ReadOnlySequence<byte> _data;
        private int _initialLength;

        /// <summary>
        /// Initialize the reader with the specified data stream.
        /// </summary>
        /// <param name="data">The stream to read from.</param>
        public JpegReader(ReadOnlySequence<byte> data)
        {
            _data = data;
            _initialLength = checked((int)data.Length);
        }

        /// <summary>
        /// Initialize the reader with the specified data stream.
        /// </summary>
        /// <param name="data">The stream to read from.</param>
        public JpegReader(ReadOnlyMemory<byte> data)
        {
            _data = new ReadOnlySequence<byte>(data);
            _initialLength = data.Length;
        }

        /// <summary>
        /// Gets whether there is any remaining data to read.
        /// </summary>
        public bool IsEmpty => _data.IsEmpty;

        /// <summary>
        /// Gets the remaining byte count for the reader to read.
        /// </summary>
        public int RemainingByteCount => (int)_data.Length;

        /// <summary>
        /// Gets the total consumed byte count from the start of the stream.
        /// </summary>
        public int ConsumedByteCount => _initialLength - (int)_data.Length;

        /// <summary>
        /// Gets the remaining data to read.
        /// </summary>
        public ReadOnlySequence<byte> RemainingBytes => _data;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryPeekToBuffer(Span<byte> buffer)
        {
#if NO_READONLYSEQUENCE_FISTSPAN
            ReadOnlySpan<byte> span = _data.First.Span;
#else
            ReadOnlySpan<byte> span = _data.FirstSpan;
#endif
            if (span.Length >= buffer.Length)
            {
                span.Slice(0, buffer.Length).CopyTo(buffer);
                return true;
            }

            return TryPeekToBufferSlow(span, buffer);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryPeekToBufferSlow(ReadOnlySpan<byte> firstSpan, Span<byte> buffer)
        {
            Debug.Assert(firstSpan.Length < buffer.Length);

            firstSpan.CopyTo(buffer);
            buffer = buffer.Slice(firstSpan.Length);

            ReadOnlySequence<byte> remaining = _data.Slice(firstSpan.Length);
            if (remaining.Length >= buffer.Length)
            {
                remaining.Slice(0, buffer.Length).CopyTo(buffer);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Read the StartOfImage marker.
        /// </summary>
        /// <returns>True if the immediately following bytes in the stream is StartOfImage marker.</returns>
        public bool TryReadStartOfImageMarker()
        {
            Span<byte> buffer = stackalloc byte[2];
            if (!TryPeekToBuffer(buffer))
            {
                return false;
            }

            if (buffer[0] == (byte)JpegMarker.Padding && buffer[1] == (byte)JpegMarker.StartOfImage)
            {
                _data = _data.Slice(2);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Read the next marker.
        /// </summary>
        /// <param name="marker">The next marker in the stream.</param>
        /// <returns>True if the immediately following bytes in the stream is a marker.</returns>
        public bool TryReadMarker(out JpegMarker marker)
        {
            Span<byte> buffer = stackalloc byte[2];

            while (TryPeekToBuffer(buffer))
            {
                byte b1 = buffer[0];
                byte b2 = buffer[1];

                if (b1 == (byte)JpegMarker.Padding)
                {
                    if (b2 == (byte)JpegMarker.Padding)
                    {
                        _data = _data.Slice(1);
                        continue;
                    }
                    else if (b2 == 0)
                    {
                        _data = _data.Slice(2);
                        continue;
                    }
                    _data = _data.Slice(2);
                    marker = (JpegMarker)b2;
                    return true;
                }

                SequencePosition? position = _data.PositionOf((byte)JpegMarker.Padding);
                if (!position.HasValue)
                {
                    _data = default;
                    marker = default;
                    return false;
                }

                _data = _data.Slice(position.GetValueOrDefault());
            }
            marker = default;
            return false;
        }

        /// <summary>
        /// Read the next two bytes as the length field.
        /// </summary>
        /// <param name="length">The length represented by the next two bytes.</param>
        /// <returns>True if the remaining stream contains at least two bytes.</returns>
        public bool TryReadLength(out ushort length)
        {
            Span<byte> buffer = stackalloc byte[2];
            if (!TryPeekToBuffer(buffer))
            {
                length = default;
                return false;
            }
            length = (ushort)(buffer[0] << 8 | buffer[1] - 2);
            _data = _data.Slice(2);
            return true;
        }

        /// <summary>
        /// Read the next two bytes as the length field, but the stream offset is not advanced.
        /// </summary>
        /// <param name="length">The length represented by the next two bytes.</param>
        /// <returns>True if the remaining stream contains at least two bytes.</returns>
        public bool TryPeekLength(out ushort length)
        {
            Span<byte> buffer = stackalloc byte[2];
            if (!TryPeekToBuffer(buffer))
            {
                length = default;
                return false;
            }
            length = (ushort)(buffer[0] << 8 | buffer[1] - 2);
            return true;
        }

        /// <summary>
        /// Read bytes from the stream.
        /// </summary>
        /// <param name="length">The length of bytes to read.</param>
        /// <param name="bytes">The bytes read from the stream.</param>
        /// <returns>True if the length of the remaining stream is not less then the <paramref name="length"/> parameter.</returns>
        public bool TryReadBytes(int length, out ReadOnlySequence<byte> bytes)
        {
            ReadOnlySequence<byte> buffer = _data;
            if (buffer.Length < length)
            {
                bytes = default;
                return false;
            }
            bytes = buffer.Slice(0, length);
            _data = buffer.Slice(length);
            return true;
        }

        /// <summary>
        /// Read bytes from the stream, but the stream offset is not advanced.
        /// </summary>
        /// <param name="length">The length of bytes to read.</param>
        /// <param name="bytes">The bytes read from the stream.</param>
        /// <returns>True if the length of the remaining stream is not less then the <paramref name="length"/> parameter.</returns>
        public bool TryPeekBytes(int length, out ReadOnlySequence<byte> bytes)
        {
            ReadOnlySequence<byte> buffer = _data;
            if (buffer.Length < length)
            {
                bytes = default;
                return false;
            }
            bytes = buffer.Slice(0, length);
            return true;
        }

        /// <summary>
        /// Advance the stream by the specified byte count.
        /// </summary>
        /// <param name="length">The byte count to advance by.</param>
        /// <returns>True if the length of the remaining stream is not less then the <paramref name="length"/> parameter.</returns>
        public bool TryAdvance(int length)
        {
            if (_data.Length < length)
            {
                return false;
            }
            _data = _data.Slice(length);
            return true;
        }
    }
}
