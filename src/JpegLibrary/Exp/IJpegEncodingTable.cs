using System;

namespace JpegLibrary
{
    internal interface IJpegEncodingTable
    {
        public ushort BytesRequired { get; }
        bool TryWrite(Span<byte> buffer, out int bytesWritten);
    }
}
