namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates a custom wire format for frames: instead of the engine's default protobuf-net, every <see cref="SampleFrame"/> crosses the
/// network as UTF-8 JSON, written into a pooled buffer. The engine never negotiates a format, so every node must state the same serializer, as every Sample node does through
/// <see cref="SampleEngineConfiguration"/>. The serializer has to rebuild the right type from the bytes alone, which is trivial here because a Sample
/// connection only ever carries <see cref="SampleFrame"/>.
/// </summary>
public sealed class SampleJsonSerializer : FrameSerializer<SampleFrame, SamplePacket>
{
    /// <inheritdoc />
    public override IMemoryOwner<byte> Serialize(SampleFrame frame)
    {
        PooledBufferWriter buffer = new();
        using (Utf8JsonWriter writer = new(buffer)) { JsonSerializer.Serialize(writer, frame); }

        return buffer.ToOwner();
    }

    /// <inheritdoc />
    public override SampleFrame Deserialize(ReadOnlyMemory<byte> data, SamplePacket? packet) => JsonSerializer.Deserialize<SampleFrame>(data.Span) ?? throw new InvalidDataException("The bytes hold no frame");
}
