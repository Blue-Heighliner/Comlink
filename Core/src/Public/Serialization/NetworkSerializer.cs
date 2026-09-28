namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Serializes and deserializes instances of the host's message type (see <see cref="IEngineBuilder.Message{TMessage}"/>)
/// to and from the bytes sent across the network. A host may substitute its own wire format entirely by supplying its
/// own implementation to <see cref="IMessageBuilder{TMessage}.Serializer"/>, as long as the same format is used
/// consistently by every node that needs to talk to this one.
/// </summary>
public interface INetworkSerializer
{
    /// <summary>
    /// Serializes <paramref name="value"/> into a pool-backed buffer.
    /// </summary>
    /// <param name="value">The message instance to serialize, an instance of the host's message type.</param>
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
    /// <param name="data">The raw, already-received message payload.</param>
    /// <returns>The deserialized instance, or <see langword="null"/> if deserialization produced no value.</returns>
    object? Deserialize(ReadOnlyMemory<byte> data);
}
