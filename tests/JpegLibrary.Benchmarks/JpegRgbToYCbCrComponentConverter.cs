using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary.Benchmarks
{
    internal class JpegRgbToYCbCrComponentConverter
    {
        public static JpegRgbToYCbCrComponentConverter Shared { get; } = new JpegRgbToYCbCrComponentConverter();

        private int[] _yRTable;
        private int[] _yGTable;
        private int[] _yBTable;
        private int[] _cbRTable;
        private int[] _cbGTable;
        private int[] _cbBTable;
        private int[] _crGTable;
        private int[] _crBTable;

        private const int ScaleBits = 16;
        private const int CBCrOffset = 128 << ScaleBits;
        private const int Half = 1 << (ScaleBits - 1);

        public JpegRgbToYCbCrComponentConverter()
        {
            _yRTable = new int[256];
            _yGTable = new int[256];
            _yBTable = new int[256];
            _cbRTable = new int[256];
            _cbGTable = new int[256];
            _cbBTable = new int[256];
            _crGTable = new int[256];
            _crBTable = new int[256];

            for (int i = 0; i < 256; i++)
            {
                // The values for the calculations are left scaled up since we must add them together before rounding.
                _yRTable[i] = Fix(0.299F) * i;
                _yGTable[i] = Fix(0.587F) * i;
                _yBTable[i] = (Fix(0.114F) * i) + Half;
                _cbRTable[i] = (-Fix(0.168735892F)) * i;
                _cbGTable[i] = (-Fix(0.331264108F)) * i;

                // We use a rounding fudge - factor of 0.5 - epsilon for Cb and Cr.
                // This ensures that the maximum output will round to 255
                // not 256, and thus that we don't have to range-limit.
                //
                // B=>Cb and R=>Cr tables are the same
                _cbBTable[i] = (Fix(0.5F) * i) + CBCrOffset + Half - 1;

                _crGTable[i] = (-Fix(0.418687589F)) * i;
                _crBTable[i] = (-Fix(0.081312411F)) * i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Fix(float x)
        {
            return (int)((x * (1L << ScaleBits)) + 0.5F);
        }

        public void ConvertRgba32ToYComponent(ReadOnlySpan<byte> rgba, ref short y, int count)
        {
            if (rgba.Length < 4 * count)
            {
                throw new ArgumentException("RGB buffer is too small.", nameof(rgba));
            }

            int[] yRTable = _yRTable;
            int[] yGTable = _yGTable;
            int[] yBTable = _yBTable;

            ref byte sourceRef = ref MemoryMarshal.GetReference(rgba);

            byte r, g, b;

            for (int i = 0; i < count; i++)
            {
                r = sourceRef;
                g = Unsafe.Add(ref sourceRef, 1);
                b = Unsafe.Add(ref sourceRef, 2);

                Unsafe.Add(ref y, i) = (short)((yRTable[r] + yGTable[g] + yBTable[b]) >> ScaleBits);

                sourceRef = ref Unsafe.Add(ref sourceRef, 4);
            }
        }

        public void ConvertRgba32ToCbComponent(ReadOnlySpan<byte> rgba, ref short cb, int count)
        {
            if (rgba.Length < 4 * count)
            {
                throw new ArgumentException("RGB buffer is too small.", nameof(rgba));
            }

            int[] cbRTable = _cbRTable;
            int[] cbGTable = _cbGTable;
            int[] cbBTable  =_cbBTable;

            ref byte sourceRef = ref MemoryMarshal.GetReference(rgba);

            byte r, g, b;

            for (int i = 0; i < count; i++)
            {
                r = sourceRef;
                g = Unsafe.Add(ref sourceRef, 1);
                b = Unsafe.Add(ref sourceRef, 2);

                Unsafe.Add(ref cb, i) = (short)((cbRTable[r] + cbGTable[g] + cbBTable[b]) >> ScaleBits);

                sourceRef = ref Unsafe.Add(ref sourceRef, 4);
            }
        }

        public void ConvertRgba32ToCrComponent(ReadOnlySpan<byte> rgba, ref short cr, int count)
        {
            if (rgba.Length < 4 * count)
            {
                throw new ArgumentException("RGB buffer is too small.", nameof(rgba));
            }

            int[] cbBTable = _cbBTable;
            int[] crGTable = _crGTable;
            int[] crBTable = _crBTable;

            ref byte sourceRef = ref MemoryMarshal.GetReference(rgba);

            byte r, g, b;

            for (int i = 0; i < count; i++)
            {
                r = sourceRef;
                g = Unsafe.Add(ref sourceRef, 1);
                b = Unsafe.Add(ref sourceRef, 2);

                Unsafe.Add(ref cr, i) = (short)((cbBTable[r] + crGTable[g] + crBTable[b]) >> ScaleBits);

                sourceRef = ref Unsafe.Add(ref sourceRef, 4);
            }
        }
    }
}
