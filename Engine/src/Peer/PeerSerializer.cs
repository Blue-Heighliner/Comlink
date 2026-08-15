namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Provides protobuf serialization and deserialization helpers for the engine's message type, whichever
/// concrete type <see cref="IEngineController"/> resolves it to. Works from the runtime <see cref="Type"/>
/// rather than a compile-time generic parameter, since the concrete type is chosen by the host.
/// </summary>
internal static class PeerSerializer
{
    /// <summary>Serializes <paramref name="value"/> into a pooled <see cref="OwnedBuffer"/>, using its runtime type.</summary>
    internal static OwnedBuffer Serialize(object value)
    {
        PooledArrayBufferWriter<byte> writer = new();
        RuntimeTypeModel.Default.Serialize(writer, value);
        return new OwnedBuffer(writer);
    }

    /// <summary>Deserializes a protobuf-encoded instance of <paramref name="type"/> from <paramref name="data"/>.</summary>
    internal static object? Deserialize(Type type, ReadOnlyMemory<byte> data)
        => Serializer.NonGeneric.Deserialize(type, data);
}
