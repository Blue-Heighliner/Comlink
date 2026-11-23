namespace BlueHeighliner.Comlink;

/// <summary>The order the bytes of a number are written in, for <see cref="BufferWriterExtensions"/> and <see cref="SpanReaderExtensions"/>. It is the same whatever order the machine itself uses, so data written on one reads back on another. The methods that take one default to <see cref="Big"/>, the order of network protocols and most file formats.</summary>
public enum ByteEndianness
{
    /// <summary>The least significant byte comes first, as on x86 and ARM machines; <c>0x01020304</c> is <c>04 03 02 01</c>.</summary>
    Little,

    /// <summary>The most significant byte comes first, as in network protocols; <c>0x01020304</c> is <c>01 02 03 04</c>.</summary>
    Big
}
