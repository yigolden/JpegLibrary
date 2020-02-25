#nullable enable

namespace JpegLibrary
{
    internal class JpegTranscodeComponent
    {
        public int ComponentIndex { get; set; }
        public int HorizontalSamplingFactor { get; set; }
        public int VerticalSamplingFactor { get; set; }
        public JpegHuffmanDecodingTable? DcTable { get; set; }
        public JpegHuffmanDecodingTable? AcTable { get; set; }
        public JpegHuffmanEncodingTableBuilder? DcTableBuilder { get; set; }
        public JpegHuffmanEncodingTableBuilder? AcTableBuilder { get; set; }
        public JpegHuffmanEncodingTable? DcEncodingTable { get; set; }
        public JpegHuffmanEncodingTable? AcEncodingTable { get; set; }
    }
}
