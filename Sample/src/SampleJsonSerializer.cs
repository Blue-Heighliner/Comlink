namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates a custom wire format for messages: instead of the engine's default protobuf-net, every <see cref="SampleFrame"/> crosses the
/// network as UTF-8 JSON. The engine never negotiates a format, so every node must state the same serializer, as every Sample node does through
/// <see cref="SampleEngineConfiguration"/>. The serializer has to rebuild the right type from the bytes alone, which is trivial here because a Sample
/// connection only ever carries <see cref="SampleFrame"/>.
/// </summary>
public sealed class SampleJsonSerializer : INetworkSerializer
{
    /// <inheritdoc />
    public IMemoryOwner<byte> Serialize(object value) => new JsonBuffer(JsonSerializer.SerializeToUtf8Bytes((SampleFrame)value));

    /// <inheritdoc />
    public object? Deserialize(ReadOnlyMemory<byte> data) => JsonSerializer.Deserialize<SampleFrame>(data.Span);

    private sealed class JsonBuffer(byte[] bytes) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = bytes;

        public void Dispose()
        {
        }
    }
}
