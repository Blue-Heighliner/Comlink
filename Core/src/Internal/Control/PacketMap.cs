namespace BlueHeighliner.Comlink.Control;

/// <summary>The engine-side form of a packet mapping: every accessor takes the packet as an <see cref="object"/>.</summary>
internal sealed class PacketMap
{
    /// <summary>The host's packet type.</summary>
    public required Type Type { get; init; }
    /// <summary>The serializer for the packet type.</summary>
    public required ServiceRegistration<IPacketSerializer> Serializer { get; init; }
    /// <summary>The largest a serialized packet may be, in bytes.</summary>
    public required int Size { get; init; }
    /// <summary>How many packets may be in flight over one connection at once.</summary>
    public required int Window { get; init; }
    /// <summary>Creates a new, empty packet.</summary>
    public required Func<object> Create { get; init; }
    /// <summary>Reads the payload identifier.</summary>
    public required Func<object, int> GetPayloadId { get; init; }
    /// <summary>Writes the payload identifier.</summary>
    public required Action<object, int> SetPayloadId { get; init; }
    /// <summary>Reads the packet index.</summary>
    public required Func<object, int> GetIndex { get; init; }
    /// <summary>Writes the packet index.</summary>
    public required Action<object, int> SetIndex { get; init; }
    /// <summary>Reads the packet count.</summary>
    public required Func<object, int> GetCount { get; init; }
    /// <summary>Writes the packet count.</summary>
    public required Action<object, int> SetCount { get; init; }
    /// <summary>Reads the payload length.</summary>
    public required Func<object, bool> GetIsData { get; init; }
    /// <summary>Writes whether the packet is a data packet.</summary>
    public required Action<object, bool> SetIsData { get; init; }
    /// <summary>Reads the payload length.</summary>
    public required Func<object, int> GetPayloadLength { get; init; }
    /// <summary>Writes the payload length.</summary>
    public required Action<object, int> SetPayloadLength { get; init; }
    /// <summary>Reads the packet data.</summary>
    public required Func<object, ReadOnlyMemory<byte>> GetData { get; init; }
    /// <summary>Writes the packet data.</summary>
    public required Action<object, ReadOnlyMemory<byte>> SetData { get; init; }
}
