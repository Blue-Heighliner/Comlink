namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Serializes and deserializes instances of the host's frame type (see <see cref="IEngineBuilder.Frames{TFrame}"/>)
/// to and from the bytes sent across the network. A host may substitute its own wire format entirely by supplying its
/// own implementation to <see cref="IFrameBuilder{TFrame}.Serializer"/>, as long as the same format is used
/// consistently by every node that needs to talk to this one.
/// </summary>
public interface INetworkSerializer
{
    /// <summary>
    /// Serializes <paramref name="value"/> into a pool-backed buffer.
    /// </summary>
    /// <param name="value">The frame instance to serialize, an instance of the host's frame type.</param>
    /// <returns>
    /// A pooled buffer holding the serialized bytes; the caller owns it and must dispose it once done, which
    /// returns the underlying memory to its pool.
    /// </returns>
    IMemoryOwner<byte> Serialize(object value);

    /// <summary>
    /// Deserializes an instance from <paramref name="data"/>, determining its own concrete type from the
    /// data itself rather than being told it - the serializer is responsible for making the wire format
    /// self-describing (see <see cref="ProtobufNetworkSerializer"/> for how the default implementation
    /// does this).
    /// </summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <returns>The deserialized instance, or <see langword="null"/> if deserialization produced no value.</returns>
    object? Deserialize(ReadOnlyMemory<byte> data);
}
