﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Copied from https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/Common/src/System/Text/ValueStringBuilder.cs

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text;

/// <summary>
///  String builder struct that allows using stack space for small strings.
/// </summary>
[InterpolatedStringHandler]
internal ref partial struct ValueStringBuilder
{
    private const int GuessedLengthPerHole = 11;
    private const int MinimumArrayPoolLength = 256;

    private char[]? _arrayToReturnToPool;

    private Span<char> _chars;
    private int _pos;

    public ValueStringBuilder(int literalLength, int formattedCount)
    {
        _arrayToReturnToPool = null;
        _chars = ArrayPool<char>.Shared.Rent(
            Math.Min(MinimumArrayPoolLength, literalLength + (GuessedLengthPerHole * formattedCount)));
        _pos = 0;
    }

    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _pos = 0;
    }

    public ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _chars = _arrayToReturnToPool;
        _pos = 0;
    }

    public int Length
    {
        readonly get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _chars.Length);
            _pos = value;
        }
    }

    public readonly int Capacity => _chars.Length;

    public void EnsureCapacity(int capacity)
    {
        // This is not expected to be called this with negative capacity
        Debug.Assert(capacity >= 0);

        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
        if ((uint)capacity > (uint)_chars.Length)
            Grow(capacity - _pos);
    }

    /// <summary>
    ///  Get a pinnable reference to the builder. Does not ensure there is a null char after <see cref="Length"/>
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This overload is pattern matched in the C# 7.3+ compiler so you can omit
    ///   the explicit method call, and write eg "fixed (char* c = builder)"
    ///  </para>
    /// </remarks>
    public readonly ref char GetPinnableReference() => ref MemoryMarshal.GetReference(_chars);

    /// <summary>
    ///  Get a pinnable reference to the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ref char GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }

        return ref MemoryMarshal.GetReference(_chars);
    }

    public ref char this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _chars[index];
        }
    }

    public override readonly string ToString() => _chars[.._pos].ToString();

    public string ToStringAndClear()
    {
        string s = ToString();
        Dispose();
        return s;
    }

    /// <summary>
    ///  Returns the underlying storage of the builder.
    /// </summary>
    public readonly Span<char> RawChars => _chars;

    /// <summary>
    ///  Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }

        return _chars[.._pos];
    }

    public readonly ReadOnlySpan<char> AsSpan() => _chars[.._pos];
    public readonly ReadOnlySpan<char> AsSpan(int start) => _chars[start.._pos];
    public readonly ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (_chars[.._pos].TryCopyTo(destination))
        {
            charsWritten = _pos;
            Dispose();
            return true;
        }
        else
        {
            charsWritten = 0;
            Dispose();
            return false;
        }
    }

    public void Insert(int index, char value, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        _chars.Slice(index, count).Fill(value);
        _pos += count;
    }

    public void Insert(int index, string? s)
    {
        if (s is null)
        {
            return;
        }

        int count = s.Length;

        if (_pos > (_chars.Length - count))
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        s.CopyTo(_chars[index..]);
        _pos += count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _pos;
        if ((uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    /// <summary>
    ///  Append a string to the builder. If the string is <see langword="null"/>, this method does nothing.
    /// </summary>
    /// <devdoc>
    ///  Name must be AppendLiteral to work with interpolated strings.
    /// </devdoc>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(string? s)
    {
        if (s is null)
        {
            return;
        }

        int pos = _pos;
        if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
        {
            // Very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
            _chars[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    private void AppendSlow(string s)
    {
        int pos = _pos;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.CopyTo(_chars[pos..]);
        _pos += s.Length;
    }

    public void AppendFormatted<TFormattable>(TFormattable value) where TFormattable : ISpanFormattable
    {
        int charsWritten;

        // This must be cast inline to avoid boxing.
        while (!((ISpanFormattable)value).TryFormat(_chars[_pos..], out charsWritten, format: default, provider: default))
        {
            Grow(1);
        }

        _pos += charsWritten;
        return;
    }

    public void AppendFormatted(string? value) => Append(value.AsSpan());

    public void AppendFormatted(object? value) => AppendLiteral(value?.ToString());

    public void Append(char c, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        Span<char> dst = _chars.Slice(_pos, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _pos += count;
    }

    public unsafe void Append(char* value, int length)
    {
        int pos = _pos;
        if (pos > _chars.Length - length)
        {
            Grow(length);
        }

        Span<char> dst = _chars.Slice(_pos, length);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }

        _pos += length;
    }

    public void Append(ReadOnlySpan<char> value)
    {
        int pos = _pos;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars[_pos..]);
        _pos += value.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    ///  Resize the internal buffer either by doubling current buffer size or
    ///  by adding <paramref name="additionalCapacityBeyondPos"/> to
    ///  <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    ///  Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
        // to double the size if possible, bounding the doubling to not go beyond the max array length.
        int newCapacity = (int)Math.Max(
            (uint)(_pos + additionalCapacityBeyondPos),
            Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        // This could also go negative if the actual required length wraps around.
        char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        _chars[.._pos].CopyTo(poolArray);

        char[]? toReturn = _arrayToReturnToPool;
        _chars = _arrayToReturnToPool = poolArray;
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        char[]? toReturn = _arrayToReturnToPool;
        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
