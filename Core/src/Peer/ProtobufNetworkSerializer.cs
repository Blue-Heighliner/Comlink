namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// The default <see cref="INetworkSerializer"/>: protobuf-net, matching <see cref="IEngineController.MessageType"/>'s
/// requirement (when this serializer is used) to carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes.
/// Every serialized value is wrapped in a single outer <see cref="ProtobufEnvelope"/> that always has the same
/// shape and records the value's runtime type by name, with the value's own protobuf-net encoding nested inside
/// it as opaque bytes - this is what lets <see cref="Deserialize"/> reconstruct the correct concrete type from
/// the data alone, without a caller needing to say what type to expect.
/// </summary>
public sealed class ProtobufNetworkSerializer : INetworkSerializer
{
    /// <inheritdoc />
    public IMemoryOwner<byte> Serialize(object value)
    {
        Type type = value.GetType();
        string typeName = type.AssemblyQualifiedName
            ?? throw new ArgumentException($"Type '{type}' has no assembly-qualified name and cannot be serialized.", nameof(value));

        using PooledArrayBufferWriter<byte> innerWriter = new();
        RuntimeTypeModel.Default.Serialize(innerWriter, value);
        ProtobufEnvelope envelope = new() { TypeName = typeName, Payload = innerWriter.WrittenMemory.ToArray() };

        PooledArrayBufferWriter<byte> writer = new();
        RuntimeTypeModel.Default.Serialize(writer, envelope);
        return new OwnedBuffer(writer);
    }

    /// <inheritdoc />
    public object? Deserialize(ReadOnlyMemory<byte> data)
    {
        ProtobufEnvelope? envelope = Serializer.Deserialize<ProtobufEnvelope>(data);
        if (envelope is null || string.IsNullOrEmpty(envelope.TypeName)) { return null; }

        Type? type = Type.GetType(envelope.TypeName);
        return type is null ? null : Serializer.NonGeneric.Deserialize(type, envelope.Payload);
    }
}

/// <summary>
/// The fixed outer wire shape every <see cref="ProtobufNetworkSerializer"/> payload is wrapped in: the
/// serialized value's runtime type, by assembly-qualified name, alongside the value's own protobuf-net
/// encoding as opaque nested bytes.
/// </summary>
[ProtoContract]
internal sealed class ProtobufEnvelope
{
    /// <summary>The serialized value's runtime type, as <see cref="Type.AssemblyQualifiedName"/>.</summary>
    [ProtoMember(1)] public string TypeName { get; set; } = string.Empty;

    /// <summary>The value's own protobuf-net encoding, opaque to the envelope itself.</summary>
    [ProtoMember(2)] public byte[] Payload { get; set; } = [];
}
