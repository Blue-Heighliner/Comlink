namespace BlueHeighliner.Comlink;

/// <summary>Which end of a byte bit number zero is, for <see cref="Bit"/>: the numbering within every byte of the data.</summary>
public enum BitEndianness
{
    /// <summary>Bit zero of a byte is its least significant bit (value 1) and bit seven its most significant (value 128). Bit 8 is then bit zero of the next byte.</summary>
    Little,

    /// <summary>Bit zero of a byte is its most significant bit (value 128) and bit seven its least significant (value 1). Bit 8 is then bit zero of the next byte.</summary>
    Big
}
