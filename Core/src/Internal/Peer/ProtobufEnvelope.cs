namespace BlueHeighliner.Comlink;

/// <summary>
/// The fixed outer wire shape every <see cref="ProtobufSerializer"/> payload is wrapped in: the
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
