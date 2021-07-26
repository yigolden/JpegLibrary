﻿#nullable enable

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace JpegLibrary
{
    /// <summary>
    /// The frame header defined by StartOfFrame marker.
    /// </summary>
    public readonly struct JpegFrameHeader
    {
        /// <summary>
        /// Initialize the frame header.
        /// </summary>
        /// <param name="samplePrecision">The sample precision.</param>
        /// <param name="numberOfLines">Number of lines.</param>
        /// <param name="samplesPerLine">Samples per line.</param>
        /// <param name="numberOfComponents">Number of components.</param>
        /// <param name="components">Parameters for each component.</param>
        public JpegFrameHeader(byte samplePrecision, ushort numberOfLines, ushort samplesPerLine, byte numberOfComponents, JpegFrameComponentSpecificationParameters[]? components)
        {
            SamplePrecision = samplePrecision;
            NumberOfLines = numberOfLines;
            SamplesPerLine = samplesPerLine;
            NumberOfComponents = numberOfComponents;
            Components = components;
        }

        /// <summary>
        /// Parameters for each component.
        /// </summary>
        public JpegFrameComponentSpecificationParameters[]? Components { get; }

        /// <summary>
        /// The sample precision.
        /// </summary>
        public byte SamplePrecision { get; }

        /// <summary>
        /// Number of lines.
        /// </summary>
        public ushort NumberOfLines { get; }

        /// <summary>
        /// Samples per line.
        /// </summary>
        public ushort SamplesPerLine { get; }

        /// <summary>
        /// Number of components.
        /// </summary>
        public byte NumberOfComponents { get; }

        /// <summary>
        /// Gets the count of bytes required to encode this frame header.
        /// </summary>
        public byte BytesRequired => (byte)(6 + 3 * NumberOfComponents);

        /// <summary>
        /// Parse the frame header from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="metadataOnly">True if the construction of the <see cref="JpegFrameComponentSpecificationParameters"/> array should be suppressed.</param>
        /// <param name="frameHeader">The frame header parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the frame header is successfully parsed.</returns>
        [SkipLocalsInit]
        public static bool TryParse(ReadOnlySequence<byte> buffer, bool metadataOnly, out JpegFrameHeader frameHeader, out int bytesConsumed)
        {
            if (buffer.IsSingleSegment)
            {
#if NO_READONLYSEQUENCE_FISTSPAN
                return TryParse(buffer.First.Span, metadataOnly, out frameHeader, out bytesConsumed);
#else
                return TryParse(buffer.FirstSpan, metadataOnly, out frameHeader, out bytesConsumed);
#endif
            }

            bytesConsumed = 0;

            if (buffer.Length < 6)
            {
                frameHeader = default;
                return false;
            }

            Span<byte> local = stackalloc byte[6];
            buffer.Slice(0, 6).CopyTo(local);

            byte numberOfComponenets = local[5];
            ushort samplesPerLine = (ushort)(local[4] | (local[3] << 8));
            ushort numberOfLines = (ushort)(local[2] | (local[1] << 8));
            byte precision = local[0];

            buffer = buffer.Slice(6);
            bytesConsumed += 6;

            if (buffer.Length < 3 * numberOfComponenets)
            {
                frameHeader = default;
                return false;
            }

            if (metadataOnly)
            {
                bytesConsumed += 3 * numberOfComponenets;
                frameHeader = new JpegFrameHeader(precision, numberOfLines, samplesPerLine, numberOfComponenets, null);
                return true;
            }

            JpegFrameComponentSpecificationParameters[] components = new JpegFrameComponentSpecificationParameters[numberOfComponenets];
            for (int i = 0; i < components.Length; i++)
            {
                if (!JpegFrameComponentSpecificationParameters.TryParse(buffer, out components[i]))
                {
                    frameHeader = default;
                    return false;
                }
                buffer = buffer.Slice(3);
                bytesConsumed += 3;
            }

            frameHeader = new JpegFrameHeader(precision, numberOfLines, samplesPerLine, numberOfComponenets, components);
            return true;
        }

        /// <summary>
        /// Parse the frame header from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="metadataOnly">True if the construction of the <see cref="JpegFrameComponentSpecificationParameters"/> array should be suppressed.</param>
        /// <param name="frameHeader">The frame header parsed.</param>
        /// <param name="bytesConsumed">The count of bytes consumed by the parser.</param>
        /// <returns>True is the frame header is successfully parsed.</returns>
        public static bool TryParse(ReadOnlySpan<byte> buffer, bool metadataOnly, out JpegFrameHeader frameHeader, out int bytesConsumed)
        {
            bytesConsumed = 0;

            if (buffer.Length < 6)
            {
                frameHeader = default;
                return false;
            }

            byte numberOfComponenets = buffer[5];
            ushort samplesPerLine = (ushort)(buffer[4] | (buffer[3] << 8));
            ushort numberOfLines = (ushort)(buffer[2] | (buffer[1] << 8));
            byte precision = buffer[0];

            buffer = buffer.Slice(6);
            bytesConsumed += 6;

            if (buffer.Length < 3 * numberOfComponenets)
            {
                frameHeader = default;
                return false;
            }

            if (metadataOnly)
            {
                bytesConsumed += 3 * numberOfComponenets;
                frameHeader = new JpegFrameHeader(precision, numberOfLines, samplesPerLine, numberOfComponenets, null);
                return true;
            }

            JpegFrameComponentSpecificationParameters[] components = new JpegFrameComponentSpecificationParameters[numberOfComponenets];
            for (int i = 0; i < components.Length; i++)
            {
                if (!JpegFrameComponentSpecificationParameters.TryParse(buffer, out components[i]))
                {
                    frameHeader = default;
                    return false;
                }
                buffer = buffer.Slice(3);
                bytesConsumed += 3;
            }

            frameHeader = new JpegFrameHeader(precision, numberOfLines, samplesPerLine, numberOfComponenets, components);
            return true;
        }

        /// <summary>
        /// Write the frame header into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesWritten">The count of bytes written.</param>
        /// <returns>True if the destination buffer is large enough.</returns>
        public bool TryWrite(Span<byte> buffer, out int bytesWritten)
        {
            if (buffer.Length < 6)
            {
                bytesWritten = 0;
                return false;
            }

            buffer[0] = SamplePrecision;
            buffer[1] = (byte)(NumberOfLines >> 8);
            buffer[2] = (byte)(NumberOfLines);
            buffer[3] = (byte)(SamplesPerLine >> 8);
            buffer[4] = (byte)(SamplesPerLine);
            buffer[5] = NumberOfComponents;
            buffer = buffer.Slice(6);
            bytesWritten = 6;

            JpegFrameComponentSpecificationParameters[]? components = Components;
            if (components is null || components.Length < NumberOfComponents)
            {
                throw new InvalidOperationException("Components are not specified.");
            }

            for (int i = 0; i < NumberOfComponents; i++)
            {
                if (!components[i].TryWrite(buffer, out int bytes))
                {
                    return false;
                }
                buffer = buffer.Slice(bytes);
                bytesWritten += bytes;
            }
            return true;
        }

        internal bool ShadowEquals(JpegFrameHeader other)
        {
            return SamplePrecision == other.SamplePrecision && NumberOfLines == other.NumberOfLines && SamplesPerLine == other.SamplesPerLine && NumberOfComponents == other.NumberOfComponents;
        }
    }

    /// <summary>
    /// Parameters for each component in the frame.
    /// </summary>
    public readonly struct JpegFrameComponentSpecificationParameters
    {
        /// <summary>
        /// Initialize the instance.
        /// </summary>
        /// <param name="identifier">The identifier of this component.</param>
        /// <param name="horizontalSamplingFactor">The horizontal sampling factor.</param>
        /// <param name="verticalSamplingFactor">The vertical sampling factor.</param>
        /// <param name="quantizationTableSelector">The quantization table selector.</param>
        public JpegFrameComponentSpecificationParameters(byte identifier, byte horizontalSamplingFactor, byte verticalSamplingFactor, byte quantizationTableSelector)
        {
            Identifier = identifier;
            HorizontalSamplingFactor = horizontalSamplingFactor;
            VerticalSamplingFactor = verticalSamplingFactor;
            QuantizationTableSelector = quantizationTableSelector;
        }

        /// <summary>
        /// The identifier of this component.
        /// </summary>
        public byte Identifier { get; }

        /// <summary>
        /// The horizontal sampling factor.
        /// </summary>
        public byte HorizontalSamplingFactor { get; }

        /// <summary>
        /// The vertical sampling factor.
        /// </summary>
        public byte VerticalSamplingFactor { get; }

        /// <summary>
        /// The quantization table selector.
        /// </summary>
        public byte QuantizationTableSelector { get; }

        /// <summary>
        /// Parse the frame component from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="component">The frame component parsed.</param>
        /// <returns>True is the frame component is successfully parsed.</returns>
        [SkipLocalsInit]
        public static bool TryParse(ReadOnlySequence<byte> buffer, out JpegFrameComponentSpecificationParameters component)
        {
            if (buffer.IsSingleSegment)
            {
#if NO_READONLYSEQUENCE_FISTSPAN
                return TryParse(buffer.First.Span, out component);
#else
                return TryParse(buffer.FirstSpan, out component);
#endif
            }

            if (buffer.Length < 3)
            {
                component = default;
                return false;
            }

            Span<byte> local = stackalloc byte[3];
            buffer.Slice(0, 3).CopyTo(local);

            byte quantizationTableSelector = local[2];
            byte samplingFactor = local[1];
            byte identifier = local[0];

            component = new JpegFrameComponentSpecificationParameters(identifier, (byte)(samplingFactor >> 4), (byte)(samplingFactor & 0xf), quantizationTableSelector);
            return true;
        }

        /// <summary>
        /// Parse the frame component from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="component">The frame component parsed.</param>
        /// <returns>True is the frame component is successfully parsed.</returns>
        public static bool TryParse(ReadOnlySpan<byte> buffer, out JpegFrameComponentSpecificationParameters component)
        {
            if (buffer.Length < 3)
            {
                component = default;
                return false;
            }

            byte quantizationTableSelector = buffer[2];
            byte samplingFactor = buffer[1];
            byte identifier = buffer[0];

            component = new JpegFrameComponentSpecificationParameters(identifier, (byte)(samplingFactor >> 4), (byte)(samplingFactor & 0xf), quantizationTableSelector);
            return true;
        }

        /// <summary>
        /// Write the frame component into the buffer specified.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesWritten">The count of bytes written.</param>
        /// <returns>True if the destination buffer is large enough.</returns>
        public bool TryWrite(Span<byte> buffer, out int bytesWritten)
        {
            if (buffer.Length < 3)
            {
                bytesWritten = 0;
                return false;
            }

            buffer[0] = Identifier;
            buffer[1] = (byte)((HorizontalSamplingFactor << 4) | (VerticalSamplingFactor & 0xf));
            buffer[2] = QuantizationTableSelector;
            bytesWritten = 3;
            return true;
        }
    }
}
