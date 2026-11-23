namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="SpanReaderExtensions"/>: what each read takes from the span, and that reading is the counterpart of <see cref="BufferWriterExtensions"/>.</summary>
public sealed class SpanReaderExtensionsTests
{
    private delegate T Read<T>(ref ReadOnlySpan<byte> span);

    private static DateTime Utc(int month, int day, int hour, int minute) => new(DateTime.UtcNow.Year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static T Reading<T>(byte[] data, Read<T> read)
    {
        ReadOnlySpan<byte> span = data;
        return read(ref span);
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>Each read takes from the front of the span and moves it past what it took, so reads follow one another.</summary>
    [Fact]
    public void Reads_AdvanceTheSpan()
    {
        ReadOnlySpan<byte> span = new byte[] { 1, 2, 3, 4, 5 };

        ReadOnlySpan<byte> first = span.ReadBytes(2);
        ReadOnlySpan<byte> second = span.ReadBytes(3);

        Assert.Equal([1, 2], first.ToArray());
        Assert.Equal([3, 4, 5], second.ToArray());
        Assert.True(span.IsEmpty);
    }

    /// <summary>Asking for more than is left, or a negative count, is refused, and the span is left where it was.</summary>
    [Fact]
    public void ReadBytes_PastTheEnd_Throws_AndLeavesTheSpan()
    {
        byte[] data = [1, 2, 3];
        ReadOnlySpan<byte> span = data;

        Assert.Throws<InvalidDataException>(() => Reading(data, (ref ReadOnlySpan<byte> s) => s.ReadBytes(4).Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reading(data, (ref ReadOnlySpan<byte> s) => s.ReadBytes(-1).Length));
        Assert.Equal(3, Reading(data, (ref ReadOnlySpan<byte> s) =>
        {
            try { s.ReadBytes(4); }
            catch (InvalidDataException) { }
            return s.Length;
        }));
        Assert.Equal(3, span.Length);
    }

    /// <summary>Text comes back without its padding, and a field starting with the empty byte reads as empty.</summary>
    [Fact]
    public void ReadText_TrimsThePad_AndEmptyIsNull()
    {
        ReadOnlySpan<byte> span = "AB   ..."u8;

        string? text = span.ReadText(5, ' ', '\0');
        string other = span.ReadText(3, '.');

        Assert.Equal("AB", text);
        Assert.Equal("", other);
        Assert.Null(Reading([0, 65, 65], (ref ReadOnlySpan<byte> s) => s.ReadText(3, ' ', '\0')));
        Assert.Equal("", Reading(Ascii("   "), (ref ReadOnlySpan<byte> s) => s.ReadText(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reading(Ascii("A"), (ref ReadOnlySpan<byte> s) => s.ReadText(0)));
    }

    /// <summary>Text is decoded with the encoding it was written with.</summary>
    [Fact]
    public void ReadText_UsesTheEncoding()
    {
        Assert.Equal("Aé", Reading([0x41, 0xE9, 0x20], (ref ReadOnlySpan<byte> s) => s.ReadText(3, encoding: Encoding.Latin1)));
        Assert.Equal("Aé", Reading([0x41, 0xC3, 0xA9], (ref ReadOnlySpan<byte> s) => s.ReadText(3, encoding: Encoding.UTF8)));
        Assert.Null(Reading([0xE9, 0xE9], (ref ReadOnlySpan<byte> s) => s.ReadText(2, ' ', 'é', Encoding.Latin1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reading([1, 2], (ref ReadOnlySpan<byte> s) => s.ReadText(2, ' ', 'é', Encoding.UTF8)));
    }

    /// <summary>A number reads back from its zero-padded field, an empty field is none, and a field that is not a number is refused.</summary>
    [Fact]
    public void ReadNumber_ParsesTheField()
    {
        ReadOnlySpan<byte> span = "00042-0042"u8;

        int positive = span.ReadNumberText(5);
        int? negative = span.ReadNumberText(5, '\0');

        Assert.Equal((42, -42), (positive, negative));
        Assert.Null(Reading([1, 1, 1], (ref ReadOnlySpan<byte> s) => s.ReadNumberText(3, '\u0001')));
        Assert.Throws<InvalidDataException>(() => Reading(Ascii("12x45"), (ref ReadOnlySpan<byte> s) => s.ReadNumberText(5)));
        Assert.Throws<InvalidDataException>(() => Reading([0, 0, 0], (ref ReadOnlySpan<byte> s) => s.ReadNumberText(3)));
    }

    /// <summary>A date reads back in UTC from the text its format describes, with the Z when asked, padded or not, and an empty field is none.</summary>
    [Fact]
    public void ReadDate_ReadsUtcWithTheFormat()
    {
        DateTime expected = Utc(11, 19, 12, 57);

        DateTime a = Reading(Ascii("11191257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 8));
        DateTime b = Reading(Ascii("11191257Z"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 9, includeTimezone: true));
        DateTime c = Reading(Ascii("11191257Z.."), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 11, '.', includeTimezone: true));

        Assert.Equal((expected, DateTimeKind.Utc), (a, a.Kind));
        Assert.Equal((expected, DateTimeKind.Utc), (b, b.Kind));
        Assert.Equal(expected, c);
        Assert.Null(Reading(new byte[9], (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 9, ' ', '\0', includeTimezone: true)));
        Assert.Null(Reading([5, 5, 5], (ref ReadOnlySpan<byte> s) => s.ReadDateText("ddHH", 3, ' ', '\u0005')));
    }

    /// <summary>A format that includes the year reads back the whole date, and one with a time zone is converted to UTC.</summary>
    [Fact]
    public void ReadDate_ReadsTheWholeDateWhenTheFormatHasIt()
    {
        DateTime whole = Reading(Ascii("2026-11-19T12:57:30"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("yyyy-MM-dd'T'HH:mm:ss", 19));
        DateTime zoned = Reading(Ascii("2026-11-19 14:57 +02:00"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("yyyy-MM-dd HH:mm zzz", 23));

        Assert.Equal((new DateTime(2026, 11, 19, 12, 57, 30, DateTimeKind.Utc), DateTimeKind.Utc), (whole, whole.Kind));
        Assert.Equal(new DateTime(2026, 11, 19, 12, 57, 0, DateTimeKind.Utc), zoned);
    }

    /// <summary>A field that does not fit the format, an empty field where a date is needed, too few bytes and a missing format are refused.</summary>
    [Fact]
    public void ReadDate_Errors()
    {
        Assert.Throws<InvalidDataException>(() => Reading(Ascii("13991257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 8)));
        Assert.Throws<InvalidDataException>(() => Reading(Ascii("11191257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("ddHHmm", 8)));
        Assert.Throws<InvalidDataException>(() => Reading(Ascii("11191257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 8, includeTimezone: true)));
        Assert.Throws<InvalidDataException>(() => Reading(new byte[8], (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 8)));
        Assert.Throws<InvalidDataException>(() => Reading(Ascii("1119"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 8)));
        Assert.Throws<ArgumentException>(() => Reading(Ascii("11191257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("", 8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reading(Ascii("11191257"), (ref ReadOnlySpan<byte> s) => s.ReadDateText("MMddHHmm", 0)));
    }

    /// <summary>Whole numbers are read in the order asked, negatives and every width included, and each read moves the span on by the width.</summary>
    [Fact]
    public void ReadIntegers_FollowTheByteOrderAsked()
    {
        ReadOnlySpan<byte> span = new byte[]
        {
            0x7F, 0xFF,
            0x01, 0x02, 0x02, 0x01,
            0xFF, 0xFE, 0xAB, 0xCD,
            0xFF, 0xFF, 0xFF, 0xFE, 0xD4, 0xC3, 0xB2, 0xA1,
            1, 2, 3, 4, 5, 6, 7, 8,
            0x01, 0, 0, 0, 0, 0, 0, 0xFF,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02,
            0x02, 0x01, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0xFF, 0xFF
        };

        byte b = span.ReadByte();
        sbyte sb = span.ReadSByte();
        short big16 = span.ReadInt16(ByteEndianness.Big);
        short little16 = span.ReadInt16(ByteEndianness.Little);
        short negative16 = span.ReadInt16(ByteEndianness.Big);
        ushort unsigned16 = span.ReadUInt16(ByteEndianness.Big);
        int negative32 = span.ReadInt32(ByteEndianness.Big);
        uint unsigned32 = span.ReadUInt32(ByteEndianness.Little);
        long long64 = span.ReadInt64(ByteEndianness.Big);
        ulong unsigned64 = span.ReadUInt64(ByteEndianness.Little);
        Int128 big128 = span.ReadInt128(ByteEndianness.Big);
        UInt128 little128 = span.ReadUInt128(ByteEndianness.Little);

        Assert.Equal((0x7F, -1), (b, sb));
        Assert.Equal((0x0102, 0x0102, -2, 0xABCD), (big16, little16, negative16, unsigned16));
        Assert.Equal((-2, 0xA1B2C3D4), (negative32, unsigned32));
        Assert.Equal((0x0102030405060708, 0xFF00000000000001), (long64, unsigned64));
        Assert.Equal((new Int128(0, 0x0102), new UInt128(0, 0x0102)), (big128, little128));
        Assert.Equal(2, span.Length);
    }

    /// <summary>Floating-point numbers are read from their IEEE 754 bits in the order asked.</summary>
    [Fact]
    public void ReadFloatingPoint_ReadsTheIeeeBits()
    {
        ReadOnlySpan<byte> span = new byte[] { 0x3C, 0x00, 0x00, 0x00, 0x80, 0x3F, 0x3F, 0xF0, 0, 0, 0, 0, 0, 0 };

        Half half = span.ReadHalf(ByteEndianness.Big);
        float single = span.ReadSingle(ByteEndianness.Little);
        double dbl = span.ReadDouble(ByteEndianness.Big);

        Assert.Equal((Half.One, 1f, 1d), (half, single, dbl));
        Assert.True(span.IsEmpty);
    }

    /// <summary>Without a byte order every number is read big-endian, whatever the machine's own order, so it reads back what the writer's default wrote.</summary>
    [Fact]
    public void ReadNumbers_DefaultToBigEndian()
    {
        ReadOnlySpan<byte> span = new byte[]
        {
            0x01, 0x02,
            0xAB, 0xCD,
            0x01, 0x02, 0x03, 0x04,
            0xA1, 0xB2, 0xC3, 0xD4,
            1, 2, 3, 4, 5, 6, 7, 8,
            0xFF, 0, 0, 0, 0, 0, 0, 0x01,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x01, 0x02,
            0x3C, 0x00,
            0x3F, 0x80, 0x00, 0x00,
            0x3F, 0xF0, 0, 0, 0, 0, 0, 0
        };

        Assert.Equal(0x0102, span.ReadInt16());
        Assert.Equal(0xABCD, span.ReadUInt16());
        Assert.Equal(0x01020304, span.ReadInt32());
        Assert.Equal(0xA1B2C3D4, span.ReadUInt32());
        Assert.Equal(0x0102030405060708, span.ReadInt64());
        Assert.Equal(0xFF00000000000001, span.ReadUInt64());
        Assert.Equal(new Int128(0, 0x0102), span.ReadInt128());
        Assert.Equal(new UInt128(0, 0x0102), span.ReadUInt128());
        Assert.Equal(Half.One, span.ReadHalf());
        Assert.Equal(1f, span.ReadSingle());
        Assert.Equal(1d, span.ReadDouble());
        Assert.True(span.IsEmpty);
    }

    /// <summary>Too few bytes for the width is refused, and so is a byte order that is not a member of the enum, which also leaves the span alone.</summary>
    [Fact]
    public void ReadNumber_TooFewBytes_OrUnknownByteOrder_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Reading([1, 2, 3], (ref ReadOnlySpan<byte> s) => s.ReadInt32(ByteEndianness.Big)));
        Assert.Throws<InvalidDataException>(() => Reading([], (ref ReadOnlySpan<byte> s) => s.ReadByte()));
        Assert.Throws<InvalidDataException>(() => Reading(new byte[15], (ref ReadOnlySpan<byte> s) => s.ReadUInt128(ByteEndianness.Little)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reading([1, 2, 3, 4], (ref ReadOnlySpan<byte> s) => s.ReadInt32((ByteEndianness)9)));
        Assert.Equal(4, Reading([1, 2, 3, 4], (ref ReadOnlySpan<byte> s) =>
        {
            try { s.ReadInt32((ByteEndianness)9); }
            catch (ArgumentOutOfRangeException) { }
            return s.Length;
        }));
    }

    /// <summary>Every number type, at its extremes, survives a write and a read in either byte order, and the order really changes the bytes.</summary>
    [Theory]
    [InlineData(ByteEndianness.Big)]
    [InlineData(ByteEndianness.Little)]
    public void Numbers_RoundTripInEitherOrder(ByteEndianness endianness)
    {
        ArrayBufferWriter<byte> writer = new();
        writer.WriteByte(byte.MaxValue);
        writer.WriteSByte(sbyte.MinValue);
        writer.WriteInt16(short.MinValue, endianness: endianness);
        writer.WriteUInt16(ushort.MaxValue, endianness: endianness);
        writer.WriteInt32(int.MinValue, endianness: endianness);
        writer.WriteUInt32(uint.MaxValue, endianness: endianness);
        writer.WriteInt64(long.MinValue, endianness: endianness);
        writer.WriteUInt64(ulong.MaxValue, endianness: endianness);
        writer.WriteInt128(Int128.MinValue, endianness: endianness);
        writer.WriteUInt128(UInt128.MaxValue, endianness: endianness);
        writer.WriteHalf(Half.MinValue, endianness: endianness);
        writer.WriteSingle(float.NegativeInfinity, endianness: endianness);
        writer.WriteDouble(Math.PI, endianness: endianness);
        writer.WriteDouble(double.NaN, endianness: endianness);
        ReadOnlySpan<byte> span = writer.WrittenSpan;

        Assert.Equal((byte.MaxValue, sbyte.MinValue), (span.ReadByte(), span.ReadSByte()));
        Assert.Equal((short.MinValue, ushort.MaxValue), (span.ReadInt16(endianness), span.ReadUInt16(endianness)));
        Assert.Equal((int.MinValue, uint.MaxValue), (span.ReadInt32(endianness), span.ReadUInt32(endianness)));
        Assert.Equal((long.MinValue, ulong.MaxValue), (span.ReadInt64(endianness), span.ReadUInt64(endianness)));
        Assert.Equal((Int128.MinValue, UInt128.MaxValue), (span.ReadInt128(endianness), span.ReadUInt128(endianness)));
        Assert.Equal((Half.MinValue, float.NegativeInfinity), (span.ReadHalf(endianness), span.ReadSingle(endianness)));
        Assert.Equal(Math.PI, span.ReadDouble(endianness));
        Assert.True(double.IsNaN(span.ReadDouble(endianness)));
        Assert.True(span.IsEmpty);
    }

    /// <summary>Everything a record is written with comes back from the reads of the same shape, in order, from a pooled writer's buffer.</summary>
    [Fact]
    public void WriteThenRead_RoundTripsARecord()
    {
        DateTime date = Utc(3, 4, 5, 6);
        PooledBufferWriter<byte> writer = new();
        writer.WriteText("STATION", 10);
        writer.WriteText(null, 4, empty: '\0');
        writer.WriteNumberText(123, 6);
        writer.WriteNumberText(null, 3, empty: '\0');
        writer.WriteDateText(date, "MMddHHmm", 12, includeTimezone: true);
        writer.WriteDateText(date, "yyyy-MM-dd", 10);
        writer.WriteInt16(-1234, endianness: ByteEndianness.Big);
        writer.WriteUInt16(60000, endianness: ByteEndianness.Little);
        writer.WriteInt32(int.MinValue, endianness: ByteEndianness.Big);
        writer.WriteByte(0xEE, repeat: 2);
        using IMemoryOwner<byte> owner = writer.ToOwner();

        ReadOnlySpan<byte> span = owner.Memory.Span;

        string station = span.ReadText(10);
        string? none = span.ReadText(4, ' ', '\0');
        int number = span.ReadNumberText(6);
        int? noNumber = span.ReadNumberText(3, '\0');
        DateTime read = span.ReadDateText("MMddHHmm", 12, includeTimezone: true);
        DateTime dayOnly = span.ReadDateText("yyyy-MM-dd", 10);
        short negative = span.ReadInt16(ByteEndianness.Big);
        ushort large = span.ReadUInt16(ByteEndianness.Little);
        int minimum = span.ReadInt32(ByteEndianness.Big);
        byte[] filler = span.ReadBytes(2).ToArray();

        Assert.Equal(("STATION", null, 123, null), (station, none, number, noNumber));
        Assert.Equal(date, read);
        Assert.Equal(date.Date, dayOnly);
        Assert.Equal((-1234, 60000, int.MinValue), (negative, large, minimum));
        Assert.Equal([0xEE, 0xEE], filler);
        Assert.True(span.IsEmpty);
    }
}
