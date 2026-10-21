namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="Bit"/>.</summary>
public sealed class BitTests
{
    /// <summary>Little endian numbers the bits of a byte from its least significant bit, and bit eight is the first bit of the next byte.</summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(15, true)]
    public void Read_Little_NumbersBitsFromTheLeastSignificantEnd(int index, bool expected) => Assert.Equal(expected, Bit.Read([0b0000_0101, 0b1000_0010], index, BitEndianness.Little));

    /// <summary>Big endian numbers the bits of a byte from its most significant bit.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(5, true)]
    [InlineData(7, true)]
    [InlineData(8, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void Read_Big_NumbersBitsFromTheMostSignificantEnd(int index, bool expected) => Assert.Equal(expected, Bit.Read([0b0000_0101, 0b1000_0010], index, BitEndianness.Big));

    /// <summary>Writing sets or clears only the one bit, in either numbering, and a bit past the first byte lands in the right byte.</summary>
    [Theory]
    [InlineData(BitEndianness.Little, 3, 0b0000_1000)]
    [InlineData(BitEndianness.Big, 3, 0b0001_0000)]
    [InlineData(BitEndianness.Little, 10, 0b0000_0100)]
    [InlineData(BitEndianness.Big, 10, 0b0010_0000)]
    public void Write_SetsAndClearsOnlyThatBit(BitEndianness endianness, int index, int mask)
    {
        byte[] data = [0, 0];
        int byteIndex = index / 8;

        Bit.Write(data, index, true, endianness);

        Assert.Equal(mask, data[byteIndex]);
        Assert.Equal(0, data[1 - byteIndex]);
        Assert.True(Bit.Read(data, index, endianness));

        Bit.Write(data, index, false, endianness);

        Assert.Equal([0, 0], data);
        Assert.False(Bit.Read(data, index, endianness));
    }

    /// <summary>Writing a bit leaves the others of its byte as they were.</summary>
    [Fact]
    public void Write_LeavesTheOtherBitsAlone()
    {
        byte[] data = [0b1111_1111];

        Bit.Write(data, 0, false, BitEndianness.Big);

        Assert.Equal(0b0111_1111, data[0]);
    }

    /// <summary>An index before the first bit or past the last one is refused.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(1000)]
    public void OutOfRange_Throws(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Bit.Read(new byte[2], index, BitEndianness.Little));
        Assert.Throws<ArgumentOutOfRangeException>(() => Bit.Write(new byte[2], index, true, BitEndianness.Big));
    }
}
