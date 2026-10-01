namespace BlueHeighliner.Comlink.Tests;

/// <summary>Serializes a <see cref="TestPacket"/> as its id, index, count and payload length in four big-endian 32 bit fields, then the data.</summary>
public sealed class RawPacketSerializer : IPacketSerializer
{
    /// <summary>Gets the number of bytes in front of the data.</summary>
    public static int HeaderSize { get; } = 16;

    /// <inheritdoc />
    public IMemoryOwner<byte> Serialize(object value, object? frame)
    {
        TestPacket packet = (TestPacket)value;
        byte[] bytes = new byte[HeaderSize + packet.Data.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes, packet.PayloadId);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), packet.Index);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), packet.Count);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(12), packet.PayloadLength);
        packet.Data.CopyTo(bytes.AsSpan(HeaderSize));
        return new ArrayOwner(bytes);
    }

    /// <inheritdoc />
    public object? Deserialize(ReadOnlyMemory<byte> data)
    {
        if (data.Length < HeaderSize) { return null; }

        ReadOnlySpan<byte> span = data.Span;
        return new TestPacket
        {
            PayloadId = BinaryPrimitives.ReadInt32BigEndian(span),
            Index = BinaryPrimitives.ReadInt32BigEndian(span[4..]),
            Count = BinaryPrimitives.ReadInt32BigEndian(span[8..]),
            PayloadLength = BinaryPrimitives.ReadInt32BigEndian(span[12..]),
            Data = span[HeaderSize..].ToArray()
        };
    }

    private sealed class ArrayOwner(byte[] bytes) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = bytes;
        public void Dispose() { }
    }
}
