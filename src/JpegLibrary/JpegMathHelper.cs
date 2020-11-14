﻿#nullable enable

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JpegLibrary
{
    internal static class JpegMathHelper
    {
#if NO_BIT_OPERATIONS
        private static ReadOnlySpan<byte> TrailingZeroCountDeBruijn => new byte[32]
        {
            00, 01, 28, 02, 29, 14, 24, 03,
            30, 22, 20, 15, 25, 17, 04, 08,
            31, 27, 13, 23, 21, 19, 16, 07,
            26, 12, 18, 06, 11, 05, 10, 09
        };

        private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31
        };
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RoundToInt32(float value)
        {
#if NO_MATHF
            return (int)Math.Round(value);
#else
            return (int)MathF.Round(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short RoundToInt16(float value)
        {
#if NO_MATHF
            return (short)Math.Round(value);
#else
            return (short)MathF.Round(value);
#endif
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Clamp(int value, int min, int max)
        {
#if NO_MATH_CLAMP
            return Math.Min(Math.Max(value, min), max);
#else
            return Math.Clamp(value, min, max);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Clamp(float value, float min, float max)
        {
#if NO_MATH_CLAMP
            return Math.Min(Math.Max(value, min), max);
#else
            return Math.Clamp(value, min, max);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2(uint value)
        {
#if NO_BIT_OPERATIONS
            return Log2SoftwareFallback(value);
#else
            return BitOperations.Log2(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value)
        {
#if NO_BIT_OPERATIONS
            if (value == 0)
            {
                return 32;
            }

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Unsafe.AddByteOffset(
                // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_0111_1100_1011_0101_0011_0001u
                ref MemoryMarshal.GetReference(TrailingZeroCountDeBruijn),
                // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                (IntPtr)(int)(((value & (uint)-(int)value) * 0x077CB531u) >> 27)); // Multi-cast mitigates redundant conv.u8
#else
            return BitOperations.TrailingZeroCount(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(ulong value)
        {
#if NO_BIT_OPERATIONS
            uint lo = (uint)value;

            if (lo == 0)
            {
                return 32 + TrailingZeroCount((uint)(value >> 32));
            }

            return TrailingZeroCount(lo);
#else
            return BitOperations.TrailingZeroCount(value);
#endif
        }

#if NO_BIT_OPERATIONS
        /// <summary>
        /// Returns the integer (floor) log of the specified value, base 2.
        /// Note that by convention, input value 0 returns 0 since Log(0) is undefined.
        /// Does not directly use any hardware intrinsics, nor does it incur branching.
        /// </summary>
        /// <param name="value">The value.</param>
        private static int Log2SoftwareFallback(uint value)
        {
            // No AggressiveInlining due to large method size
            // Has conventional contract 0->0 (Log(0) is undefined)

            // Fill trailing zeros with ones, eg 00010010 becomes 00011111
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Unsafe.AddByteOffset(
                // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
                ref MemoryMarshal.GetReference(Log2DeBruijn),
                // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
        }
#endif

    }
}
