#if (NETFRAMEWORK || NETSTANDARD2_0 || NETCOREAPP2_0)

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System
{
    public static class ArgumentOutOfRangeExceptionEx
    {
        [DoesNotReturn]
        private static void ThrowZero<T>(T value, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeNonZero, {paramName}, {value}");

        [DoesNotReturn]
        private static void ThrowNegative<T>(T value, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeNonNegative, {paramName}, {value}");

        [DoesNotReturn]
        private static void ThrowNegativeOrZero<T>(T value, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeNonNegativeNonZero, {paramName}, {value}");

        [DoesNotReturn]
        private static void ThrowGreater<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeLessOrEqual, {paramName}, {value}, {other}");

        [DoesNotReturn]
        private static void ThrowGreaterEqual<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeLess, {paramName}, {value}, {other}");

        [DoesNotReturn]
        private static void ThrowLess<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeGreaterOrEqual, {paramName}, {value}, {other}");

        [DoesNotReturn]
        private static void ThrowLessEqual<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeGreater, {paramName}, {value}, {other}");

        [DoesNotReturn]
        private static void ThrowEqual<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeNotEqual, {paramName}, {(object?)value ?? "null"}, {(object?)other ?? "null"}");

        [DoesNotReturn]
        private static void ThrowNotEqual<T>(T value, T other, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, $"ArgumentOutOfRange_Generic_MustBeEqual, {paramName}, {(object?)value ?? "null"}, {(object?)other ?? "null"}");

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is zero.</summary>
        /// <param name="value">The argument to validate as non-zero.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value == 0)
                ThrowZero(value, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative.</summary>
        /// <param name="value">The argument to validate as non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < 0)
                ThrowNegative(value, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative or zero.</summary>
        /// <param name="value">The argument to validate as non-zero or non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNegativeOrZero(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value <= 0)
                ThrowNegativeOrZero(value, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as not equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : IEquatable<T>?
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
                ThrowEqual(value, other, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNotEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : IEquatable<T>?
        {
            if (!EqualityComparer<T>.Default.Equals(value, other))
                ThrowNotEqual(value, other, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as less or equal than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfGreaterThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0)
                ThrowGreater(value, other, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is greater than or equal <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as less than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfGreaterThanOrEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) >= 0)
                ThrowGreaterEqual(value, other, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as greatar than or equal than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfLessThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
                ThrowLess(value, other, paramName);
        }

        /// <summary>Throws an <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than or equal <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as greatar than than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfLessThanOrEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) <= 0)
                ThrowLessEqual(value, other, paramName);
        }
    }
}

#endif