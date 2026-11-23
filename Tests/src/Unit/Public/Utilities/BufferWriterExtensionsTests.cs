namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="BufferWriterExtensions"/>: the bytes each write puts into the writer.</summary>
public sealed class BufferWriterExtensionsTests
{
    private static byte[] Written(Action<ArrayBufferWriter<byte>> write)
    {
        ArrayBufferWriter<byte> writer = new();
        write(writer);
        return writer.WrittenSpan.ToArray();
    }

    private static DateTime Utc(int month, int day, int hour, int minute) => new(DateTime.UtcNow.Year, month, day, hour, minute, 0, DateTimeKind.Utc);

    /// <summary>Every write can be repeated: none at all for zero, and a negative count is refused before anything is written.</summary>
    [Fact]
    public void Repeat_WritesTheValueThatManyTimes()
    {
        Assert.Equal([7, 7, 7], Written(w => w.WriteByte(7, 3)));
        Assert.Equal([0, 0], Written(w => w.WriteByte(0, repeat: 2)));
        Assert.Empty(Written(w => w.WriteByte(1, 0)));
        Assert.Equal([0, 1, 0, 1], Written(w => w.WriteInt16(1, repeat: 2)));
        Assert.Equal([1, 0, 1, 0], Written(w => w.WriteInt16(1, 2, ByteEndianness.Little)));
        Assert.Equal("AB  AB  "u8.ToArray(), Written(w => w.WriteText("AB", 4, repeat: 2)));
        Assert.Equal("ABAB"u8.ToArray(), Written(w => w.WriteText("AB", repeat: 2)));
        Assert.Equal("007007"u8.ToArray(), Written(w => w.WriteNumberText(7, 3, repeat: 2)));
        Assert.Equal("1912.1912."u8.ToArray(), Written(w => w.WriteDateText(Utc(11, 19, 12, 57), "ddHH", 5, '.', repeat: 2)));
        Assert.Equal("--".Select(c => (byte)c).ToArray(), Written(w => w.WriteText(null, 1, empty: '-', repeat: 2)));
        Assert.Empty(Written(w => w.WriteText("AB", 4, repeat: 0)));
    }

    /// <summary>A negative repeat or a bad argument writes nothing, even with a repeat of zero.</summary>
    [Fact]
    public void Repeat_Errors_WriteNothing()
    {
        ArrayBufferWriter<byte> writer = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteByte(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteInt32(1, repeat: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteText("A", repeat: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteInt32(1, 0, (ByteEndianness)7));
        Assert.Throws<ArgumentException>(() => writer.WriteText("too long", 3, repeat: 0));

        Assert.Equal(0, writer.WrittenCount);
    }

    /// <summary>Text fills its field, the rest padded, and nothing is written past the field.</summary>
    [Fact]
    public void WriteText_FillsTheFieldWithPad()
    {
        Assert.Equal("AB   "u8.ToArray(), Written(w => w.WriteText("AB", 5)));
        Assert.Equal("AB..."u8.ToArray(), Written(w => w.WriteText("AB", 5, '.')));
        Assert.Equal("ABCDE"u8.ToArray(), Written(w => w.WriteText("ABCDE", 5)));
        Assert.Equal("A?"u8.ToArray(), Written(w => w.WriteText("Aé", 2)));
    }

    /// <summary>No text fills the whole field with the empty byte, and text that does not fit, a missing text or a bad length is refused.</summary>
    [Fact]
    public void WriteText_EmptyAndErrors()
    {
        Assert.Equal([0, 0, 0], Written(w => w.WriteText(null, 3, empty: '\0')));
        Assert.Equal([9, 9], Written(w => w.WriteText(null, 2, empty: '\t')));
        Assert.Throws<ArgumentException>(() => new ArrayBufferWriter<byte>().WriteText("ABCDEF", 5));
        Assert.Throws<ArgumentNullException>(() => new ArrayBufferWriter<byte>().WriteText(null, 5));
        Assert.Throws<ArgumentException>(() => new ArrayBufferWriter<byte>().WriteText(null, empty: '-'));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayBufferWriter<byte>().WriteText("A", 3, 'é'));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayBufferWriter<byte>().WriteText(null, 3, empty: 'é'));
        Assert.Equal("ABC"u8.ToArray(), Written(w => w.WriteText("ABC")));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayBufferWriter<byte>().WriteText("A", 0));
    }

    /// <summary>The text is written in the encoding asked, ASCII without one, and a pad or empty character that is not one byte in it is refused.</summary>
    [Fact]
    public void WriteText_UsesTheEncoding()
    {
        Assert.Equal([0x41, 0xC3, 0xA9], Written(w => w.WriteText("Aé", encoding: Encoding.UTF8)));
        Assert.Equal([0x41, 0xE9, 0x20], Written(w => w.WriteText("Aé", 3, encoding: Encoding.Latin1)));
        Assert.Equal([0xE9, 0xE9], Written(w => w.WriteText(null, 2, empty: 'é', encoding: Encoding.Latin1)));
        Assert.Equal("012"u8.ToArray(), Written(w => w.WriteNumberText(12, 3, encoding: Encoding.UTF8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayBufferWriter<byte>().WriteText("A", 3, 'é', encoding: Encoding.UTF8));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ArrayBufferWriter<byte>().WriteText("A", 3, encoding: Encoding.Unicode));
    }

    /// <summary>A failed write leaves the writer untouched.</summary>
    [Fact]
    public void FailedWrite_WritesNothing()
    {
        ArrayBufferWriter<byte> writer = new();

        Assert.Throws<ArgumentException>(() => writer.WriteText("too long", 3));

        Assert.Equal(0, writer.WrittenCount);
    }

    /// <summary>A number is zero-padded to its field, a negative one counts its sign, none fills the field with the empty byte, and one that does not fit is refused.</summary>
    [Fact]
    public void WriteNumber_ZeroPadsToTheField()
    {
        Assert.Equal("00042"u8.ToArray(), Written(w => w.WriteNumberText(42, 5)));
        Assert.Equal("-0042"u8.ToArray(), Written(w => w.WriteNumberText(-42, 5)));
        Assert.Equal([1, 1, 1], Written(w => w.WriteNumberText(null, 3, empty: '\u0001')));
        Assert.Throws<ArgumentException>(() => new ArrayBufferWriter<byte>().WriteNumberText(123456, 5));
        Assert.Equal("42"u8.ToArray(), Written(w => w.WriteNumberText(42)));
        Assert.Equal("-7"u8.ToArray(), Written(w => w.WriteNumberText(-7)));
        Assert.Throws<ArgumentNullException>(() => new ArrayBufferWriter<byte>().WriteNumberText(null, 3));
        Assert.Throws<ArgumentException>(() => new ArrayBufferWriter<byte>().WriteNumberText(-12345, 5));
    }

    /// <summary>A date is written as the text its format gives, with a Z after it when asked, padded to its field, and none fills the field with the empty byte.</summary>
    [Fact]
    public void WriteDate_WritesTheFormattedTextInItsField()
    {
        DateTime date = Utc(11, 19, 12, 57);

        Assert.Equal("11191257"u8.ToArray(), Written(w => w.WriteDateText(date, "MMddHHmm", 8)));
        Assert.Equal("11191257Z"u8.ToArray(), Written(w => w.WriteDateText(date, "MMddHHmm", 9, includeTimezone: true)));
        Assert.Equal("191257Z "u8.ToArray(), Written(w => w.WriteDateText(date, "ddHHmm", 8, includeTimezone: true)));
        Assert.Equal("191257.."u8.ToArray(), Written(w => w.WriteDateText(date, "ddHHmm", 8, '.')));
        Assert.Equal("1912"u8.ToArray(), Written(w => w.WriteDateText(date, "ddHH", 4)));
        Assert.Equal(new byte[9], Written(w => w.WriteDateText(null, "MMddHHmm", 9, empty: '\0', includeTimezone: true)));
        Assert.Equal([5, 5, 5], Written(w => w.WriteDateText(null, "ddHH", 3, empty: '\u0005')));
    }

    /// <summary>The format is the invariant culture's whatever the thread's culture is, so the same date is always the same bytes.</summary>
    [Fact]
    public void WriteDate_IgnoresTheCurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            Assert.Equal("Nov 19"u8.ToArray(), Written(w => w.WriteDateText(Utc(11, 19, 1, 1), "MMM dd", 6)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>A date not in UTC, a missing format, a bad length and text that does not fit are refused, and nothing is written.</summary>
    [Fact]
    public void WriteDate_Errors_WriteNothing()
    {
        ArrayBufferWriter<byte> writer = new();
        DateTime date = Utc(11, 19, 12, 57);

        Assert.Throws<ArgumentException>(() => writer.WriteDateText(DateTime.SpecifyKind(date, DateTimeKind.Local), "MMddHHmm", 8));
        Assert.Throws<ArgumentException>(() => writer.WriteDateText(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), "MMddHHmm", 8, empty: '-'));
        Assert.Throws<ArgumentException>(() => writer.WriteDateText(date, "", 8));
        Assert.Throws<ArgumentNullException>(() => writer.WriteDateText(date, null!, 8));
        Assert.Throws<ArgumentNullException>(() => writer.WriteDateText(null, "MMddHHmm", 8));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteDateText(date, "MMddHHmm", 0));
        Assert.Throws<ArgumentException>(() => writer.WriteDateText(date, "MMddHHmm", 7));
        Assert.Throws<ArgumentException>(() => writer.WriteDateText(date, "MMddHHmm", 8, includeTimezone: true));
        Assert.Equal(0, writer.WrittenCount);
    }

    /// <summary>Whole numbers are written in the order asked, whatever the machine's own, negatives and every width included.</summary>
    [Fact]
    public void WriteIntegers_FollowTheByteOrderAsked()
    {
        Assert.Equal([0x7F], Written(w => w.WriteByte(0x7F)));
        Assert.Equal([0xFF], Written(w => w.WriteSByte(-1)));
        Assert.Equal([0x01, 0x02], Written(w => w.WriteInt16(0x0102, endianness: ByteEndianness.Big)));
        Assert.Equal([0x02, 0x01], Written(w => w.WriteInt16(0x0102, endianness: ByteEndianness.Little)));
        Assert.Equal([0xFF, 0xFE], Written(w => w.WriteInt16(-2, endianness: ByteEndianness.Big)));
        Assert.Equal([0xAB, 0xCD], Written(w => w.WriteUInt16(0xABCD, endianness: ByteEndianness.Big)));
        Assert.Equal([0xCD, 0xAB], Written(w => w.WriteUInt16(0xABCD, endianness: ByteEndianness.Little)));
        Assert.Equal([0x01, 0x02, 0x03, 0x04], Written(w => w.WriteInt32(0x01020304, endianness: ByteEndianness.Big)));
        Assert.Equal([0x04, 0x03, 0x02, 0x01], Written(w => w.WriteInt32(0x01020304, endianness: ByteEndianness.Little)));
        Assert.Equal([0xFF, 0xFF, 0xFF, 0xFE], Written(w => w.WriteInt32(-2, endianness: ByteEndianness.Big)));
        Assert.Equal([0xA1, 0xB2, 0xC3, 0xD4], Written(w => w.WriteUInt32(0xA1B2C3D4, endianness: ByteEndianness.Big)));
        Assert.Equal([0xD4, 0xC3, 0xB2, 0xA1], Written(w => w.WriteUInt32(0xA1B2C3D4, endianness: ByteEndianness.Little)));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], Written(w => w.WriteInt64(0x0102030405060708, endianness: ByteEndianness.Big)));
        Assert.Equal([8, 7, 6, 5, 4, 3, 2, 1], Written(w => w.WriteInt64(0x0102030405060708, endianness: ByteEndianness.Little)));
        Assert.Equal([0xFF, 0, 0, 0, 0, 0, 0, 0x01], Written(w => w.WriteUInt64(0xFF00000000000001, endianness: ByteEndianness.Big)));
        Assert.Equal([0x01, 0, 0, 0, 0, 0, 0, 0xFF], Written(w => w.WriteUInt64(0xFF00000000000001, endianness: ByteEndianness.Little)));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02], Written(w => w.WriteInt128(0x0102, endianness: ByteEndianness.Big)));
        Assert.Equal([0x02, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], Written(w => w.WriteUInt128(0x0102, endianness: ByteEndianness.Little)));
        Assert.Equal(Enumerable.Repeat((byte)0xFF, 16), Written(w => w.WriteInt128(-1, endianness: ByteEndianness.Big)));
    }

    /// <summary>Without a byte order every number is written big-endian, whatever the machine's own order.</summary>
    [Fact]
    public void WriteNumbers_DefaultToBigEndian()
    {
        Assert.Equal([0x01, 0x02], Written(w => w.WriteInt16(0x0102)));
        Assert.Equal([0xAB, 0xCD], Written(w => w.WriteUInt16(0xABCD)));
        Assert.Equal([0x01, 0x02, 0x03, 0x04], Written(w => w.WriteInt32(0x01020304)));
        Assert.Equal([0xA1, 0xB2, 0xC3, 0xD4], Written(w => w.WriteUInt32(0xA1B2C3D4)));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], Written(w => w.WriteInt64(0x0102030405060708)));
        Assert.Equal([0xFF, 0, 0, 0, 0, 0, 0, 0x01], Written(w => w.WriteUInt64(0xFF00000000000001)));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02], Written(w => w.WriteInt128(0x0102)));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02], Written(w => w.WriteUInt128(0x0102)));
        Assert.Equal([0x3C, 0x00], Written(w => w.WriteHalf(Half.One)));
        Assert.Equal([0x3F, 0x80, 0x00, 0x00], Written(w => w.WriteSingle(1f)));
        Assert.Equal([0x3F, 0xF0, 0, 0, 0, 0, 0, 0], Written(w => w.WriteDouble(1d)));
    }

    /// <summary>Floating-point numbers are written as their IEEE 754 bits in the order asked.</summary>
    [Fact]
    public void WriteFloatingPoint_WritesTheIeeeBits()
    {
        Assert.Equal([0x3C, 0x00], Written(w => w.WriteHalf(Half.One, endianness: ByteEndianness.Big)));
        Assert.Equal([0x00, 0x3C], Written(w => w.WriteHalf(Half.One, endianness: ByteEndianness.Little)));
        Assert.Equal([0x3F, 0x80, 0x00, 0x00], Written(w => w.WriteSingle(1f, endianness: ByteEndianness.Big)));
        Assert.Equal([0x00, 0x00, 0x80, 0x3F], Written(w => w.WriteSingle(1f, endianness: ByteEndianness.Little)));
        Assert.Equal([0x3F, 0xF0, 0, 0, 0, 0, 0, 0], Written(w => w.WriteDouble(1d, endianness: ByteEndianness.Big)));
        Assert.Equal([0, 0, 0, 0, 0, 0, 0xF0, 0x3F], Written(w => w.WriteDouble(1d, endianness: ByteEndianness.Little)));
    }

    /// <summary>A byte order that is not a member of the enum is refused and nothing is written.</summary>
    [Fact]
    public void WriteNumber_UnknownByteOrder_Throws_AndWritesNothing()
    {
        ArrayBufferWriter<byte> writer = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteInt32(1, endianness: (ByteEndianness)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteDouble(1d, endianness: (ByteEndianness)7));

        Assert.Equal(0, writer.WrittenCount);
    }

    /// <summary>The writes work on any buffer writer, a pooled one included, and follow one another.</summary>
    [Fact]
    public void Writes_FollowOneAnother_OnAPooledWriter()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 4);

        writer.WriteInt16(1, endianness: ByteEndianness.Big);
        writer.WriteText("OK", 4);
        writer.WriteByte(9, repeat: 2);
        using IMemoryOwner<byte> owner = writer.ToOwner();

        Assert.Equal(new byte[] { 0, 1, (byte)'O', (byte)'K', 32, 32, 9, 9 }, owner.Memory.ToArray());
    }
}
