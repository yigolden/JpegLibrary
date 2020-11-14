using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JpegLibrary
{
    internal ref struct JpegScanHeaderWriter
    {
        private ReadOnlySpan<JpegScanComponentSpecificationParameters> _components;

        /// <summary>
        /// The number of component in this scan.
        /// </summary>
        public byte NumberOfComponents => (byte)_components.Length;

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

        /// <summary>
        /// Gets the count of bytes required to encode this scan header.
        /// </summary>
        public byte BytesRequired => (byte)(4 + 2 * NumberOfComponents);

        public JpegScanHeaderWriter(ReadOnlySpan<JpegScanComponentSpecificationParameters> components, byte startOfSpectralSelection, byte endOfSpectralSelection, byte successiveApproximationBitPositionHigh, byte successiveApproximationBitPositionLow)
        {
            _components = components;
            StartOfSpectralSelection = startOfSpectralSelection;
            EndOfSpectralSelection = endOfSpectralSelection;
            SuccessiveApproximationBitPositionHigh = successiveApproximationBitPositionHigh;
            SuccessiveApproximationBitPositionLow = successiveApproximationBitPositionLow;
        }

        public bool TryWrite(Span<byte> buffer, out int bytesWritten)
        {
            if (buffer.IsEmpty)
            {
                bytesWritten = 0;
                return false;
            }

            buffer[0] = NumberOfComponents;
            buffer = buffer.Slice(1);
            bytesWritten = 1;

            ReadOnlySpan<JpegScanComponentSpecificationParameters> components = _components;

            for (int i = 0; i < NumberOfComponents; i++)
            {
                if (!components[i].TryWrite(buffer, out int bytes))
                {
                    return false;
                }
                buffer = buffer.Slice(bytes);
                bytesWritten += bytes;
            }

            if (buffer.Length < 3)
            {
                return false;
            }

            buffer[0] = StartOfSpectralSelection;
            buffer[1] = EndOfSpectralSelection;
            buffer[2] = (byte)((SuccessiveApproximationBitPositionHigh << 4) | (SuccessiveApproximationBitPositionLow & 0xf));
            bytesWritten += 3;

            return true;
        }
    }
}
