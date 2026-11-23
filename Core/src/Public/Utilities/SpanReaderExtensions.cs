namespace BlueHeighliner.Comlink;

/// <summary>
/// Extensions for reading fixed-layout data from a <see cref="ReadOnlySpan{T}"/> of bytes, the counterparts of <see cref="BufferWriterExtensions"/>. The span is the reading position: each call reads from the start of it and moves it past
/// what was read, as <c>BinaryReader</c> does with a stream, so the same span variable is passed on from one read to the next. Reading never allocates but for the text and numbers it returns.
/// </summary>
public static class SpanReaderExtensions
{
    extension(ref ReadOnlySpan<byte> span)
    {
        /// <summary>Takes the next <paramref name="count"/> bytes and moves the span past them.</summary>
        /// <param name="count">How many bytes to read.</param>
        /// <returns>The bytes, a slice of the span itself.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="count"/> bytes left.</exception>
        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (span.Length < count)
            {
                throw new InvalidDataException($"{count} bytes are needed but only {span.Length} are left.");
            }

            ReadOnlySpan<byte> taken = span[..count];
            span = span[count..];
            return taken;
        }

        /// <summary>Reads a field of <paramref name="length"/> bytes written by <c>WriteText</c>: the text without its trailing <paramref name="pad"/>.</summary>
        /// <param name="length">How many bytes the field takes, at least one.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> is not one byte in the encoding.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left.</exception>
        public string ReadText(int length, char pad = ' ', Encoding? encoding = null) => ReadField(ref span, length, pad, null, encoding) ?? string.Empty;

        /// <summary>
        /// Reads a field of <paramref name="length"/> bytes written by <c>WriteText</c> with an <paramref name="empty"/> character: the text without its trailing <paramref name="pad"/>, or <see langword="null"/> when the field begins with
        /// <paramref name="empty"/>. Text that itself begins with that character reads as empty, so pick one the text never starts with.
        /// </summary>
        /// <param name="length">How many bytes the field takes, at least one.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="empty">The character an empty field is filled with.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The text, or <see langword="null"/> for an empty field.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left.</exception>
        public string? ReadText(int length, char pad, char empty, Encoding? encoding = null) => ReadField(ref span, length, pad, empty, encoding);

        /// <summary>Reads a field of <paramref name="length"/> bytes written by <c>WriteNumberText</c>.</summary>
        /// <param name="length">How many bytes the field takes, at least one.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left, or the field is not a number.</exception>
        public int ReadNumberText(int length, Encoding? encoding = null) => ParseNumber(ReadField(ref span, length, ' ', null, encoding));

        /// <summary>Reads a field of <paramref name="length"/> bytes written by <c>WriteNumberText</c> with an <paramref name="empty"/> character: the number, or <see langword="null"/> for an empty field.</summary>
        /// <param name="length">How many bytes the field takes, at least one.</param>
        /// <param name="empty">The character an empty field is filled with.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The number, or <see langword="null"/> for an empty field.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left, or the field is not a number.</exception>
        public int? ReadNumberText(int length, char empty, Encoding? encoding = null) => ReadField(ref span, length, ' ', empty, encoding) is { } text ? ParseNumber(text) : null;

        /// <summary>
        /// Reads a field of <paramref name="length"/> bytes written by <c>WriteDateText</c>: the date its text, without the trailing <paramref name="pad"/>, parses to with <paramref name="format"/>, in UTC. What the format leaves out, such as a year, comes
        /// from the defaults of <see cref="DateTime.ParseExact(string, string, IFormatProvider)"/>.
        /// </summary>
        /// <param name="format">The custom date and time format the date was written with, such as <c>MMddHHmm</c>.</param>
        /// <param name="length">How many bytes the field takes, at least one, the <c>Z</c> included.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="includeTimezone">Whether the text ends with the <c>Z</c> that says the date is UTC.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The date.</returns>
        /// <exception cref="ArgumentException"><paramref name="format"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> is not one byte in the encoding.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left, or the field does not fit the format, or has no <c>Z</c> when <paramref name="includeTimezone"/> says it should.</exception>
        public DateTime ReadDateText(string format, int length, char pad = ' ', bool includeTimezone = false, Encoding? encoding = null)
            => ParseDate(ReadField(ref span, length, pad, null, encoding) ?? string.Empty, format, includeTimezone);

        /// <summary>
        /// Reads a field of <paramref name="length"/> bytes written by <c>WriteDateText</c> with an <paramref name="empty"/> character: the date, or <see langword="null"/> when the field begins with <paramref name="empty"/>.
        /// </summary>
        /// <param name="format">The custom date and time format the date was written with, such as <c>MMddHHmm</c>.</param>
        /// <param name="length">How many bytes the field takes, at least one, the <c>Z</c> included.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="empty">The character an empty field is filled with.</param>
        /// <param name="includeTimezone">Whether the text ends with the <c>Z</c> that says the date is UTC.</param>
        /// <param name="encoding">How the bytes become text, the one the field was written with, or <see langword="null"/> for ASCII.</param>
        /// <returns>The date, or <see langword="null"/> for an empty field.</returns>
        /// <exception cref="ArgumentException"><paramref name="format"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than <paramref name="length"/> bytes left, or the field does not fit the format, or has no <c>Z</c> when <paramref name="includeTimezone"/> says it should.</exception>
        public DateTime? ReadDateText(string format, int length, char pad, char empty, bool includeTimezone = false, Encoding? encoding = null)
            => ReadField(ref span, length, pad, empty, encoding) is { } text ? ParseDate(text, format, includeTimezone) : null;

        /// <summary>Reads one byte.</summary>
        /// <returns>The number.</returns>
        /// <exception cref="InvalidDataException">The span is empty.</exception>
        public byte ReadByte() => ReadInteger<byte>(ref span, isUnsigned: true);

        /// <summary>Reads one byte, two's complement.</summary>
        /// <returns>The number.</returns>
        /// <exception cref="InvalidDataException">The span is empty.</exception>
        public sbyte ReadSByte() => ReadInteger<sbyte>(ref span, isUnsigned: false);

        /// <summary>Reads 2 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 2 bytes left.</exception>
        public short ReadInt16(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<short>(ref span, isUnsigned: false, endianness);

        /// <summary>Reads 2 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 2 bytes left.</exception>
        public ushort ReadUInt16(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<ushort>(ref span, isUnsigned: true, endianness);

        /// <summary>Reads 4 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 4 bytes left.</exception>
        public int ReadInt32(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<int>(ref span, isUnsigned: false, endianness);

        /// <summary>Reads 4 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 4 bytes left.</exception>
        public uint ReadUInt32(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<uint>(ref span, isUnsigned: true, endianness);

        /// <summary>Reads 8 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 8 bytes left.</exception>
        public long ReadInt64(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<long>(ref span, isUnsigned: false, endianness);

        /// <summary>Reads 8 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 8 bytes left.</exception>
        public ulong ReadUInt64(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<ulong>(ref span, isUnsigned: true, endianness);

        /// <summary>Reads 16 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 16 bytes left.</exception>
        public Int128 ReadInt128(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<Int128>(ref span, isUnsigned: false, endianness);

        /// <summary>Reads 16 bytes written in the given order as a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 16 bytes left.</exception>
        public UInt128 ReadUInt128(ByteEndianness endianness = ByteEndianness.Big) => ReadInteger<UInt128>(ref span, isUnsigned: true, endianness);

        /// <summary>Reads 2 bytes written in the given order as the IEEE 754 bits of a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 2 bytes left.</exception>
        public Half ReadHalf(ByteEndianness endianness = ByteEndianness.Big) => BitConverter.UInt16BitsToHalf(ReadInteger<ushort>(ref span, isUnsigned: true, endianness));

        /// <summary>Reads 4 bytes written in the given order as the IEEE 754 bits of a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 4 bytes left.</exception>
        public float ReadSingle(ByteEndianness endianness = ByteEndianness.Big) => BitConverter.UInt32BitsToSingle(ReadInteger<uint>(ref span, isUnsigned: true, endianness));

        /// <summary>Reads 8 bytes written in the given order as the IEEE 754 bits of a number.</summary>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <returns>The number.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>.</exception>
        /// <exception cref="InvalidDataException">The span has fewer than 8 bytes left.</exception>
        public double ReadDouble(ByteEndianness endianness = ByteEndianness.Big) => BitConverter.UInt64BitsToDouble(ReadInteger<ulong>(ref span, isUnsigned: true, endianness));
    }

    private static string? ReadField(ref ReadOnlySpan<byte> span, int length, char pad, char? empty, Encoding? encoding)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        encoding ??= Encoding.ASCII;
        if (empty is { } emptyCharacter)
        {
            BufferWriterExtensions.RequireSingleByte(emptyCharacter, encoding, nameof(empty));
        }

        ReadOnlySpan<byte> field = span.ReadBytes(length);
        return empty is { } marker && field[0] == BufferWriterExtensions.SingleByte(marker, encoding) ? null : encoding.GetString(field).TrimEnd(pad);
    }

    private static int ParseNumber(string? text)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : throw new InvalidDataException($"\"{text}\" is not a number.");

    private static DateTime ParseDate(string text, string format, bool includeTimezone)
    {
        ArgumentException.ThrowIfNullOrEmpty(format);
        if (includeTimezone)
        {
            text = text.EndsWith('Z') ? text[..^1] : throw new InvalidDataException($"\"{text}\" does not end with the Z of a UTC date.");
        }

        return DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date) ? date : throw new InvalidDataException($"\"{text}\" is not a date in the format {format}.");
    }

    private static T ReadInteger<T>(ref ReadOnlySpan<byte> span, bool isUnsigned, ByteEndianness endianness = ByteEndianness.Big) where T : System.Numerics.IBinaryInteger<T>
    {
        if (endianness is not (ByteEndianness.Big or ByteEndianness.Little))
        {
            throw new ArgumentOutOfRangeException(nameof(endianness), endianness, "The byte order is not known.");
        }

        ReadOnlySpan<byte> bytes = span.ReadBytes(T.Zero.GetByteCount());
        return endianness is ByteEndianness.Big ? T.ReadBigEndian(bytes, isUnsigned) : T.ReadLittleEndian(bytes, isUnsigned);
    }
}
