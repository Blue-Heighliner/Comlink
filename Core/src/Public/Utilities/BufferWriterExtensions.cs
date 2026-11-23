namespace BlueHeighliner.Comlink;

/// <summary>
/// Extensions for writing fixed-layout data into an <see cref="IBufferWriter{T}"/> of bytes: text and numbers, padded to a fixed-width field that may be empty or not, dates as formatted text, and numbers of every primitive type in either byte order. Every write can be repeated. They write straight into the
/// span the writer offers and never allocate. Each is read back by its counterpart in <see cref="SpanReaderExtensions"/>.
/// </summary>
public static class BufferWriterExtensions
{
    extension(IBufferWriter<byte> writer)
    {
        /// <summary>
        /// Writes <paramref name="text"/> in <paramref name="encoding"/>. With no <paramref name="length"/> it is written as it is; with one it fills a field of exactly that many bytes, the unused end filled with <paramref name="pad"/>. With <paramref name="empty"/> a
        /// <see langword="null"/> text is allowed and fills the whole field with that character, which is how an empty field is told from a field of padding. A character the encoding cannot represent is written as its replacement, <c>?</c> for ASCII.
        /// </summary>
        /// <param name="text">The text, or <see langword="null"/> for an empty field, which needs <paramref name="empty"/> and <paramref name="length"/>.</param>
        /// <param name="length">How many bytes the field takes, at least one, or <see langword="null"/> to write only the text.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="empty">The character that fills the whole field when there is no text, or <see langword="null"/> when the text is required.</param>
        /// <param name="encoding">How the text becomes bytes, or <see langword="null"/> for ASCII. <paramref name="pad"/> and <paramref name="empty"/> must each be one byte in it, so a field of <paramref name="length"/> bytes can be filled with them.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/> and there is no <paramref name="empty"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="text"/> is longer than <paramref name="length"/>, or is <see langword="null"/> and there is no <paramref name="length"/> to fill.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeat"/> is negative.</exception>
        public void WriteText(string? text, int? length = null, char pad = ' ', char? empty = null, Encoding? encoding = null, int repeat = 1) => WriteField(writer, text, length, pad, empty, encoding, repeat);

        /// <summary>
        /// Writes <paramref name="number"/> as decimal digit text in <paramref name="encoding"/>. With no <paramref name="length"/> they are written as they are; with one they fill a field of exactly that many bytes, with zeros in front and a minus sign, which counts toward the field,
        /// before the digits of a negative number. With <paramref name="empty"/> a <see langword="null"/> number is allowed and fills the whole field with that character.
        /// </summary>
        /// <param name="number">The number, or <see langword="null"/> for an empty field, which needs <paramref name="empty"/> and <paramref name="length"/>.</param>
        /// <param name="length">How many bytes the field takes, at least one, or <see langword="null"/> to write only the digits.</param>
        /// <param name="empty">The character that fills the whole field when there is no number, or <see langword="null"/> when the number is required.</param>
        /// <param name="encoding">How the text becomes bytes, or <see langword="null"/> for ASCII. <paramref name="empty"/> must be one byte in it, so a field of <paramref name="length"/> bytes can be filled with them.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="number"/> is <see langword="null"/> and there is no <paramref name="empty"/>.</exception>
        /// <exception cref="ArgumentException">The number needs more than <paramref name="length"/> bytes, or is <see langword="null"/> and there is no <paramref name="length"/> to fill.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeat"/> is negative.</exception>
        public void WriteNumberText(int? number, int? length = null, char? empty = null, Encoding? encoding = null, int repeat = 1)
            => WriteField(writer, number?.ToString(length is { } width ? $"D{(number < 0 ? width - 1 : width)}" : "D", CultureInfo.InvariantCulture), length, ' ', empty, encoding, repeat, nameof(number));

        /// <summary>
        /// Writes <paramref name="date"/> as text in <paramref name="encoding"/> formatted with <paramref name="format"/>, followed by <c>Z</c>, which says the date is UTC, when <paramref name="includeTimezone"/> is set. With no <paramref name="length"/> only the text is
        /// written; with one it fills a field of exactly that many bytes, the <c>Z</c> included, the unused end filled with <paramref name="pad"/>. With <paramref name="empty"/> a <see langword="null"/> date is allowed and fills the whole field with
        /// that character. The text is formatted with the invariant culture and takes only what the format names, so a format with no year gives a date that reads back with the year the reader's defaults give it.
        /// </summary>
        /// <param name="date">The date, in UTC, or <see langword="null"/> for an empty field, which needs <paramref name="empty"/> and <paramref name="length"/>.</param>
        /// <param name="format">A custom date and time format, such as <c>MMddHHmm</c>.</param>
        /// <param name="length">How many bytes the field takes, at least one, or <see langword="null"/> to write only the text.</param>
        /// <param name="pad">The character that fills what the text leaves of the field.</param>
        /// <param name="empty">The character that fills the whole field when there is no date, or <see langword="null"/> when the date is required.</param>
        /// <param name="includeTimezone">Whether a <c>Z</c> follows the formatted text.</param>
        /// <param name="encoding">How the text becomes bytes, or <see langword="null"/> for ASCII. <paramref name="pad"/> and <paramref name="empty"/> must each be one byte in it, so a field of <paramref name="length"/> bytes can be filled with them.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <exception cref="ArgumentNullException"><paramref name="date"/> is <see langword="null"/> and there is no <paramref name="empty"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="format"/> is empty, <paramref name="date"/> is not in UTC, the text is longer than <paramref name="length"/>, or the date is <see langword="null"/> and there is no <paramref name="length"/> to fill.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive, or <paramref name="pad"/> or <paramref name="empty"/> is not one byte in the encoding.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeat"/> is negative.</exception>
        public void WriteDateText(DateTime? date, string format, int? length = null, char pad = ' ', char? empty = null, bool includeTimezone = false, Encoding? encoding = null, int repeat = 1)
        {
            ArgumentException.ThrowIfNullOrEmpty(format);
            if (date is { Kind: not DateTimeKind.Utc })
            {
                throw new ArgumentException("The date must be in UTC.", nameof(date));
            }

            WriteField(writer, date is { } value ? value.ToString(format, CultureInfo.InvariantCulture) + (includeTimezone ? "Z" : string.Empty) : null, length, pad, empty, encoding, repeat, nameof(date));
        }

        /// <summary>Writes <paramref name="value"/> as one byte.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeat"/> is negative.</exception>
        public void WriteByte(byte value, int repeat = 1) => WriteInteger(writer, value, repeat: repeat);

        /// <summary>Writes <paramref name="value"/> as one byte, two's complement.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="repeat"/> is negative.</exception>
        public void WriteSByte(sbyte value, int repeat = 1) => WriteInteger(writer, value, repeat: repeat);

        /// <summary>Writes <paramref name="value"/> as 2 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteInt16(short value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 2 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteUInt16(ushort value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 4 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteInt32(int value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 4 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteUInt32(uint value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 8 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteInt64(long value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 8 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteUInt64(ulong value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 16 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteInt128(Int128 value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 16 bytes in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteUInt128(UInt128 value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, value, endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 2 bytes, its IEEE 754 bits, in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteHalf(Half value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, BitConverter.HalfToUInt16Bits(value), endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 4 bytes, its IEEE 754 bits, in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteSingle(float value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, BitConverter.SingleToUInt32Bits(value), endianness, repeat);

        /// <summary>Writes <paramref name="value"/> as 8 bytes, its IEEE 754 bits, in the given order.</summary>
        /// <param name="value">The number.</param>
        /// <param name="repeat">How many times to write it, one unless stated; zero writes nothing.</param>
        /// <param name="endianness">Which byte comes first; most significant first (big-endian, the order of network protocols) unless stated.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="endianness"/> is not a member of <see cref="ByteEndianness"/>, or <paramref name="repeat"/> is negative.</exception>
        public void WriteDouble(double value, int repeat = 1, ByteEndianness endianness = ByteEndianness.Big) => WriteInteger(writer, BitConverter.DoubleToUInt64Bits(value), endianness, repeat);
    }

    private static void WriteField(IBufferWriter<byte> writer, string? text, int? length, char pad, char? empty, Encoding? encoding, int repeat, string argument = "text")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(repeat);
        encoding ??= Encoding.ASCII;
        if (length is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "The field must take at least one byte.");
        }

        if (length is not null)
        {
            RequireSingleByte(pad, encoding, nameof(pad));
        }

        if (empty is { } emptyCharacter)
        {
            RequireSingleByte(emptyCharacter, encoding, nameof(empty));
        }

        if (text is null)
        {
            if (empty is not { } fill)
            {
                throw new ArgumentNullException(argument, "A value is required unless an empty character says how to write none.");
            }

            if (length is not { } emptyLength)
            {
                throw new ArgumentException("Writing none needs a length to fill with the empty character.", argument);
            }

            int emptyTotal = checked(emptyLength * repeat);
            writer.GetSpan(emptyTotal)[..emptyTotal].Fill(SingleByte(fill, encoding));
            writer.Advance(emptyTotal);
            return;
        }

        int textLength = encoding.GetByteCount(text);
        int fieldLength = length ?? textLength;
        if (textLength > fieldLength)
        {
            throw new ArgumentException($"The text is longer than the field of {fieldLength} bytes it is written in.", argument);
        }

        int total = checked(fieldLength * repeat);
        Span<byte> span = writer.GetSpan(total);
        for (int index = 0; index < repeat; index++)
        {
            Span<byte> field = span.Slice(index * fieldLength, fieldLength);
            int written = encoding.GetBytes(text, field);
            field[written..].Fill(SingleByte(pad, encoding));
        }

        writer.Advance(total);
    }

    internal static byte SingleByte(char character, Encoding encoding)
    {
        Span<byte> bytes = stackalloc byte[8];
        return encoding.TryGetBytes([character], bytes, out int count) && count is 1 && encoding.GetString(bytes[..1])[0] == character ? bytes[0] : throw new ArgumentOutOfRangeException(nameof(character), character, "The character must be one byte in the encoding.");
    }

    internal static void RequireSingleByte(char character, Encoding encoding, string name)
    {
        Span<byte> bytes = stackalloc byte[8];
        if (!encoding.TryGetBytes([character], bytes, out int count) || count is not 1 || encoding.GetString(bytes[..1])[0] != character)
        {
            throw new ArgumentOutOfRangeException(name, character, "The character must be one byte in the encoding.");
        }
    }

    private static void WriteInteger<T>(IBufferWriter<byte> writer, T value, ByteEndianness endianness = ByteEndianness.Big, int repeat = 1) where T : System.Numerics.IBinaryInteger<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(repeat);
        if (!Enum.IsDefined(endianness))
        {
            throw new ArgumentOutOfRangeException(nameof(endianness), endianness, "The byte order is not known.");
        }

        int size = value.GetByteCount();
        Span<byte> span = writer.GetSpan(checked(size * repeat));
        for (int index = 0; index < repeat; index++)
        {
            Span<byte> slot = span.Slice(index * size, size);
            if (endianness is ByteEndianness.Big)
            {
                value.WriteBigEndian(slot);
            }
            else
            {
                value.WriteLittleEndian(slot);
            }
        }

        writer.Advance(size * repeat);
    }
}
