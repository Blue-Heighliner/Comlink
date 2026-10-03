namespace BlueHeighliner.Comlink;

/// <summary>
/// Serializes and deserializes instances of the host's frame type (see <see cref="IEngineBuilder.Frames{TFrame}"/>) to and from the bytes sent across
/// the network. A host may substitute its own wire format entirely by stating its own implementation with <see cref="IFrameBuilder{TFrame}.Serializer{TSerializer}"/>,
/// as long as the same format is used consistently by every node that needs to talk to this one. Derive from <see cref="FrameSerializer{TFrame, TPacket}"/> to work with the
/// frame and packet types rather than <see cref="object"/>.
/// </summary>
public interface IFrameSerializer
{
    /// <summary>Serializes <paramref name="frame"/> into a pool-backed buffer.</summary>
    /// <param name="frame">The frame instance to serialize, an instance of the host's frame type.</param>
    /// <returns>
    /// A pooled buffer holding the serialized bytes; the caller owns it and must dispose it once done, which
    /// returns the underlying memory to its pool.
    /// </returns>
    IMemoryOwner<byte> Serialize(object frame);

    /// <summary>
    /// Called on every outgoing frame packet (one the frame packet handler creates) with the frame it carries a piece of, before the packet is serialized, to
    /// set the packet's own properties from the frame. Called once per packet, in order, so a frame cut into several packets configures each of them.
    /// </summary>
    /// <param name="frame">The original frame, an instance of the host's frame type.</param>
    /// <param name="packet">The packet to configure, an instance of the host's packet type.</param>
    void ConfigurePacket(object frame, object packet);

    /// <summary>
    /// Deserializes a frame from <paramref name="data"/>, determining its own concrete type from the data itself rather than being told it: the serializer is
    /// responsible for making the wire format self-describing (see <see cref="ProtobufSerializer"/> for how the default implementation does this).
    /// </summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <param name="packet">The first packet (an instance of the host's packet type) that carried this frame across, or <see langword="null"/> when the frame did not travel in packets (packetization is disabled, or it arrived over an interface connection).</param>
    /// <returns>The deserialized frame.</returns>
    /// <exception cref="InvalidDataException"><paramref name="data"/> is not a frame this serializer can or will build.</exception>
    object Deserialize(ReadOnlyMemory<byte> data, object? packet);
}

/// <summary>The base class for a host's own <see cref="IFrameSerializer"/>, working with the frame and packet types rather than <see cref="object"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
public abstract class FrameSerializer<TFrame, TPacket> : IFrameSerializer
    where TFrame : class
    where TPacket : class
{
    /// <summary>Sets the properties of a frame packet from the frame it carries a piece of. Does nothing unless overridden.</summary>
    /// <param name="frame">The original frame.</param>
    /// <param name="packet">The packet to configure.</param>
    public virtual void ConfigurePacket(TFrame frame, TPacket packet)
    {
    }

    /// <summary>Serializes <paramref name="frame"/> into a pool-backed buffer the caller disposes.</summary>
    /// <param name="frame">The frame to serialize.</param>
    public abstract IMemoryOwner<byte> Serialize(TFrame frame);

    /// <summary>Deserializes a frame from <paramref name="data"/>.</summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <param name="packet">The first packet that carried this frame across, or <see langword="null"/> when it did not travel in packets.</param>
    /// <returns>The deserialized frame.</returns>
    /// <exception cref="InvalidDataException"><paramref name="data"/> is not a frame this serializer can or will build.</exception>
    public abstract TFrame Deserialize(ReadOnlyMemory<byte> data, TPacket? packet);

    /// <inheritdoc />
    void IFrameSerializer.ConfigurePacket(object frame, object packet) => ConfigurePacket((TFrame)frame, (TPacket)packet);

    /// <inheritdoc />
    IMemoryOwner<byte> IFrameSerializer.Serialize(object frame) => Serialize((TFrame)frame);

    /// <inheritdoc />
    object IFrameSerializer.Deserialize(ReadOnlyMemory<byte> data, object? packet) => Deserialize(data, (TPacket?)packet);
}
