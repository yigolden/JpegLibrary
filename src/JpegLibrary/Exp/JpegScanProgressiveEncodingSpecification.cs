namespace JpegLibrary.Exp
{
    public readonly struct JpegScanProgressiveEncodingSpecification
    {
        /// <summary>
        /// Start of spectral selection.
        /// </summary>
        public byte StartOfSpectralSelection { get; }

        /// <summary>
        /// End of spectral selection.
        /// </summary>
        public byte EndOfSpectralSelection { get; }

        /// <summary>
        /// Successive approximation bit position (high).
        /// </summary>
        public byte SuccessiveApproximationBitPositionHigh { get; }

        /// <summary>
        /// Successive approximation bit position (low).
        /// </summary>
        public byte SuccessiveApproximationBitPositionLow { get; }

        public JpegScanProgressiveEncodingSpecification(byte startOfSpectralSelection, byte endOfSpectralSelection, byte successiveApproximationBitPositionHigh, byte successiveApproximationBitPositionLow)
        {
            StartOfSpectralSelection = startOfSpectralSelection;
            EndOfSpectralSelection = endOfSpectralSelection;
            SuccessiveApproximationBitPositionHigh = successiveApproximationBitPositionHigh;
            SuccessiveApproximationBitPositionLow = successiveApproximationBitPositionLow;
        }
    }
}
