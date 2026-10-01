namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// The default <see cref="IFrameSerializer"/> and <see cref="IPacketSerializer"/>: protobuf-net, matching the frame or packet type given to
/// <see cref="IEngineBuilder.Frames{TFrame}"/> or <see cref="IEngineBuilder.Packets{TPacket}"/>, which (when this serializer is used) is required to carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes.
/// Every serialized value is wrapped in a single outer <see cref="ProtobufEnvelope"/> that always has the same
/// shape and records the value's runtime type by name, with the value's own protobuf-net encoding nested inside
/// it as opaque bytes - this is what lets <see cref="Deserialize(ReadOnlyMemory{byte})"/> reconstruct the correct concrete type from
/// the data alone, without a caller needing to say what type to expect. Since the sender chooses that name, only
/// types the serializer was told about are built, or, when it was told none, <c>[ProtoContract]</c> types.
/// </summary>
public sealed class ProtobufSerializer : IFrameSerializer, IPacketSerializer
{
    /// <summary>Initializes a serializer.</summary>
    /// <param name="knownTypes">
    /// The only types <see cref="Deserialize(ReadOnlyMemory{byte})"/> will build. The type an envelope names comes from the remote sender, so
    /// without this it would build whatever <c>[ProtoContract]</c> type the sender names that can be loaded here; the
    /// engine passes the one type it expects (see <see cref="IFrameBuilder{TFrame}.Serializer"/>), which
    /// leaves a sender nothing to choose and nothing to load.
    /// </param>
    public ProtobufSerializer(params Type[] knownTypes)
    {
        if (knownTypes.Length > 0) { this.knownTypes = knownTypes.ToDictionary(type => type.AssemblyQualifiedName ?? type.FullName ?? type.Name); }
    }

    private readonly Dictionary<string, Type>? knownTypes;

    /// <inheritdoc />
    public void ConfigurePacket(object frame, object packet)
    {
    }

    /// <inheritdoc />
    public IMemoryOwner<byte> Serialize(object value, object? frame) => Serialize(value);

    /// <inheritdoc />
    public object? Deserialize(ReadOnlyMemory<byte> data, object? packet) => Deserialize(data);

    /// <summary>Serializes <paramref name="value"/>, a frame or a packet, into a pool-backed buffer the caller disposes.</summary>
    /// <param name="value">What to serialize.</param>
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

    /// <summary>Deserializes a frame or a packet from <paramref name="data"/>, building only the types this serializer was told about.</summary>
    /// <param name="data">The raw payload.</param>
    public object? Deserialize(ReadOnlyMemory<byte> data)
    {
        ProtobufEnvelope? envelope = Serializer.Deserialize<ProtobufEnvelope>(data);
        if (envelope is null || string.IsNullOrEmpty(envelope.TypeName)) { return null; }

        Type? type = Resolve(envelope.TypeName);
        return type is null ? null : Serializer.NonGeneric.Deserialize(type, envelope.Payload);
    }

    private Type? Resolve(string typeName)
    {
        if (knownTypes is not null) { return knownTypes.GetValueOrDefault(typeName); }

        Type? type = Type.GetType(typeName);
        return type is not null && type.IsDefined(typeof(ProtoContractAttribute), inherit: false) ? type : null;
    }
}
