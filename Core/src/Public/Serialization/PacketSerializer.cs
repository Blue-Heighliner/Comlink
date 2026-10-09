namespace BlueHeighliner.Comlink;

/// <summary>
/// Serializes and deserializes instances of the host's packet type (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Packets"/>) to and from the bytes sent across
/// the network. State your own with <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Serializer{TSerializer}"/>; every node on a network must use a matching one. Derive from
/// <see cref="PacketSerializer{TFrame, TPacket}"/> to work with the packet and frame types rather than <see cref="object"/>.
/// </summary>
public interface IPacketSerializer
{
    /// <summary>Serializes <paramref name="packet"/> into a pool-backed buffer.</summary>
    /// <param name="packet">The packet instance to serialize, an instance of the host's packet type.</param>
    /// <param name="frame">The original frame (an instance of the host's frame type) this packet is one of the pieces of, or <see langword="null"/> for a packet that does not carry a frame (such as one sent by a handshake handler).</param>
    /// <returns>A pooled buffer holding the serialized bytes; the caller owns it and must dispose it once done.</returns>
    IMemoryOwner<byte> Serialize(object packet, object? frame);

    /// <summary>Deserializes a packet from <paramref name="data"/>, determining its own concrete type from the data itself rather than being told it.</summary>
    /// <param name="data">The raw, already-received packet.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="InvalidDataException"><paramref name="data"/> is not a packet this serializer can or will build.</exception>
    object Deserialize(ReadOnlyMemory<byte> data);

}

/// <summary>The base class for a host's own <see cref="IPacketSerializer"/>, working with the packet and frame types rather than <see cref="object"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public abstract class PacketSerializer<TFrame, TPacket> : IPacketSerializer
    where TFrame : class
    where TPacket : class
{
    /// <summary>Serializes <paramref name="packet"/> into a pool-backed buffer the caller disposes.</summary>
    /// <param name="packet">The packet to serialize.</param>
    /// <param name="frame">The original frame this packet is one of the pieces of, or <see langword="null"/> when it carries no frame.</param>
    public abstract IMemoryOwner<byte> Serialize(TPacket packet, TFrame? frame);

    /// <summary>Deserializes a packet from <paramref name="data"/>.</summary>
    /// <param name="data">The raw, already-received packet.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="InvalidDataException"><paramref name="data"/> is not a packet this serializer can or will build.</exception>
    public abstract TPacket Deserialize(ReadOnlyMemory<byte> data);

    /// <inheritdoc />
    IMemoryOwner<byte> IPacketSerializer.Serialize(object packet, object? frame) => Serialize((TPacket)packet, (TFrame?)frame);

    /// <inheritdoc />
    object IPacketSerializer.Deserialize(ReadOnlyMemory<byte> data) => Deserialize(data);
}
