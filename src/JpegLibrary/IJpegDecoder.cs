using System.Buffers;

namespace JpegLibrary
{
    internal interface IJpegDecoder<TWriter> where TWriter : notnull, IJpegBlockOutputWriter
    {
        MemoryPool<byte>? MemoryPool { get; }
        JpegHuffmanDecodingTable? GetHuffmanTable(bool isDcTable, byte identifier);
        JpegArithmeticDecodingTable? GetArithmeticTable(bool isDcTable, byte identifier);
        JpegQuantizationTable GetQuantizationTable(byte identifier);
        ushort GetRestartInterval();
        TWriter GetOutputWriter();
    }
}
