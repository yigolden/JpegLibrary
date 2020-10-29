namespace JpegLibrary
{
    internal class JpegEncodingComponent
    {
        internal int ComponentIndex { get; set; }
        internal byte HorizontalSamplingFactor { get; set; }
        internal byte VerticalSamplingFactor { get; set; }
        internal JpegQuantizationTable QuantizationTable { get; set; }
    }
}
