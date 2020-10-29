namespace JpegLibrary.Exp
{
    internal readonly struct JpegCoefficientsTransformContext
    {
        public byte HorizontalSamplingFactor { get; }
        public byte VerticalSamplingFactor { get; }
        public byte HorizontalSubsamplingFactor { get; }
        public byte VerticalSubsamplingFactor { get; }
        public JpegQuantizationTable QuantizationTable { get; }

        public JpegCoefficientsTransformContext(byte horizontalSamplingFactor, byte verticalSamplingFactor, int maxHorizontalSampling, int maxVerticalSampling, JpegQuantizationTable quantizationTable)
        {
            HorizontalSamplingFactor = horizontalSamplingFactor;
            VerticalSamplingFactor = verticalSamplingFactor;
            HorizontalSubsamplingFactor = (byte)(maxHorizontalSampling / horizontalSamplingFactor);
            VerticalSubsamplingFactor = (byte)(maxVerticalSampling / verticalSamplingFactor);
            QuantizationTable = quantizationTable;
        }
    }
}
