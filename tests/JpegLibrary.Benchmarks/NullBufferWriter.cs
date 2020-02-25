using System;
using System.Buffers;
using System.Diagnostics;

namespace JpegLibrary.Benchmarks
{
    internal sealed class NullBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[] _buffer = Array.Empty<byte>();

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (sizeHint <= 0)
            {
                if (_buffer.Length == 0)
                {
                    _buffer = ArrayPool<byte>.Shared.Rent(4096);
                }
                return _buffer;
            }
            Debug.Assert(sizeHint > 0);
            if (_buffer.Length < sizeHint)
            {
                if (_buffer.Length != 0)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }
                _buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
            }
            return _buffer;
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            if (sizeHint <= 0)
            {
                if (_buffer.Length == 0)
                {
                    _buffer = ArrayPool<byte>.Shared.Rent(4096);
                }
                return _buffer;
            }
            Debug.Assert(sizeHint > 0);
            if (_buffer.Length < sizeHint)
            {
                if (_buffer.Length != 0)
                {
                    ArrayPool<byte>.Shared.Return(_buffer);
                }
                _buffer = ArrayPool<byte>.Shared.Rent(sizeHint);
            }
            return _buffer;
        }

        public void Advance(int count)
        {
            if (count > _buffer.Length)
            {
                throw new InvalidOperationException();
            }
            // No op
        }

        public void Dispose()
        {
            if (_buffer.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(_buffer);
            }
            _buffer = Array.Empty<byte>();
        }
    }
}
