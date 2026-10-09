namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates a custom wire format for frames: instead of the engine's default protobuf-net, every <see cref="Frame"/> crosses the
/// network as UTF-8 JSON, written into a pooled buffer. The engine never negotiates a format, so every node must state the same serializer, as every Sample node does through
/// <see cref="EngineConfiguration"/>. The serializer has to rebuild the right type from the bytes alone, which is trivial here because a Sample
/// connection only ever carries <see cref="Frame"/>.
/// </summary>
public sealed class FrameSerializer : FrameSerializer<Frame, Packet>
{
    /// <inheritdoc />
    public override IMemoryOwner<byte> Serialize(Frame frame)
    {
        PooledBufferWriter buffer = new();
        using (Utf8JsonWriter writer = new(buffer)) { System.Text.Json.JsonSerializer.Serialize(writer, frame); }

        return buffer.ToOwner();
    }

    /// <inheritdoc />
    public override Frame Deserialize(ReadOnlyMemory<byte> data, Packet? packet) => System.Text.Json.JsonSerializer.Deserialize<Frame>(data.Span) ?? throw new InvalidDataException("The bytes hold no frame");
}
