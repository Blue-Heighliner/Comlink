namespace BlueHeighliner.Comlink.Tests;

/// <summary>A frame serializer over the default protobuf one that records the frames it deserializes.</summary>
public sealed class TrackingFrameSerializer : FrameSerializer<TestFrame, TestPacket>
{
    private readonly ProtobufSerializer inner = new();

    /// <summary>Gets the frames that were deserialized.</summary>
    public ConcurrentQueue<TestFrame> Deserialized { get; } = [];

    /// <inheritdoc />
    public override IMemoryOwner<byte> Serialize(TestFrame frame) => inner.Serialize(frame);

    /// <inheritdoc />
    public override TestFrame Deserialize(ReadOnlyMemory<byte> data, TestPacket? packet)
    {
        TestFrame frame = (TestFrame)inner.Deserialize(data, null);
        Deserialized.Enqueue(frame);
        return frame;
    }
}
