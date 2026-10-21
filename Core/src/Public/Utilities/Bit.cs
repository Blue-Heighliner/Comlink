namespace BlueHeighliner.Comlink;

/// <summary>Reads and writes single bits of a span of bytes, numbering the bits across the whole span so an index may be greater than 7: bit 8 is the first bit of the second byte, and so on.</summary>
public static class Bit
{
    /// <summary>Reads the bit at <paramref name="index"/> of <paramref name="data"/>.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The zero-based bit index across <paramref name="data"/>.</param>
    /// <param name="endianness">Which end of a byte bit zero is.</param>
    /// <returns><see langword="true"/> if the bit is set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or past the last bit of <paramref name="data"/>.</exception>
    public static bool Read(ReadOnlySpan<byte> data, int index, BitEndianness endianness)
        => (data[ByteOf(data.Length, index)] & MaskOf(index, endianness)) != 0;

    /// <summary>Writes <paramref name="value"/> to the bit at <paramref name="index"/> of <paramref name="data"/>, leaving every other bit as it is.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The zero-based bit index across <paramref name="data"/>.</param>
    /// <param name="value"><see langword="true"/> to set the bit, <see langword="false"/> to clear it.</param>
    /// <param name="endianness">Which end of a byte bit zero is.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or past the last bit of <paramref name="data"/>.</exception>
    public static void Write(Span<byte> data, int index, bool value, BitEndianness endianness)
    {
        int position = ByteOf(data.Length, index);
        byte mask = MaskOf(index, endianness);
        data[position] = value ? (byte)(data[position] | mask) : (byte)(data[position] & ~mask);
    }

    private static int ByteOf(int length, int index)
        => index >= 0 && index / 8 < length ? index / 8 : throw new ArgumentOutOfRangeException(nameof(index), index, $"The bit index must be between 0 and {(long)length * 8 - 1}.");

    private static byte MaskOf(int index, BitEndianness endianness) => (byte)(endianness is BitEndianness.Little ? 1 << (index % 8) : 0x80 >> (index % 8));
}
