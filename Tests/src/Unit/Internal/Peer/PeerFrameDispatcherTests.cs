namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerFrameDispatcher"/>'s classification of received bytes.</summary>
public sealed class PeerFrameDispatcherTests
{
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");

    private static ReadOnlyMemory<byte> Encode(IEngineController controller, TestFrame message)
    {
        using IMemoryOwner<byte> buf = controller.FrameSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>An ordinary message is delivered.</summary>
    [Fact]
    public async Task Dispatch_OrdinaryMessage_IsDelivered()
    {
        TestEngineController controller = new();
        List<object> delivered = [];

        bool ok = await PeerFrameDispatcher.Dispatch(Encode(controller, new TestFrame { MessageId = "M1" }), controller, logger, m => { delivered.Add(m); return Task.CompletedTask; }, null, null);

        Assert.True(ok);
        Assert.Single(delivered);
    }

    /// <summary>The first packet that carried a frame is handed to the frame serializer along with the bytes.</summary>
    [Fact]
    public async Task Dispatch_PacketArgument_IsHandedToTheFrameSerializer()
    {
        Mock<IFrameSerializer> serializer = new();
        TestFrame frame = new() { MessageId = "M1" };
        object packet = new();
        serializer.Setup(s => s.Deserialize(It.IsAny<ReadOnlyMemory<byte>>(), packet)).Returns(frame);
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.FrameSerializer).Returns(serializer.Object);
        List<object> delivered = [];

        bool ok = await PeerFrameDispatcher.Dispatch(new byte[] { 1 }, controller.Object, logger, m => { delivered.Add(m); return Task.CompletedTask; }, null, null, packet);

        Assert.True(ok);
        Assert.Same(frame, Assert.Single(delivered));
    }

    /// <summary>A heartbeat, an empty message, is acknowledged and neither delivered nor treated as a receipt.</summary>
    [Fact]
    public async Task Dispatch_Heartbeat_IsAcknowledgedAndIgnored()
    {
        TestEngineController controller = new();
        List<object> delivered = [];
        int receipts = 0;

        bool ok = await PeerFrameDispatcher.Dispatch(
            TestHeartbeat.Bytes(), controller, logger,
            m => { delivered.Add(m); return Task.CompletedTask; }, (_, _) => { receipts++; return Task.CompletedTask; }, null);

        Assert.True(ok);
        Assert.Empty(delivered);
        Assert.Equal(0, receipts);
    }

    /// <summary>An empty payload is not a serialized message, so it is refused rather than treated as a heartbeat.</summary>
    [Fact]
    public async Task Dispatch_EmptyPayload_IsRefused()
    {
        TestEngineController controller = new();

        Assert.False(await PeerFrameDispatcher.Dispatch(ReadOnlyMemory<byte>.Empty, controller, logger, null, null, null));
    }

    /// <summary>A retrieval request is neither delivered as a message nor treated as a receipt: only a storage server answers one.</summary>
    [Fact]
    public async Task Dispatch_RetrievalRequest_IsIgnored()
    {
        TestEngineController controller = new();
        List<object> delivered = [];
        int receipts = 0;

        bool ok = await PeerFrameDispatcher.Dispatch(
            Encode(controller, new TestFrame { MessageId = "R1", IsRetrieval = true }), controller, logger,
            m => { delivered.Add(m); return Task.CompletedTask; }, (_, _) => { receipts++; return Task.CompletedTask; }, null);

        Assert.True(ok);
        Assert.Empty(delivered);
        Assert.Equal(0, receipts);
    }

    /// <summary>A receive receipt raises only the receive receipt callback with the message id and sender.</summary>
    [Fact]
    public async Task Dispatch_ReceiveReceipt_RaisesReceiveReceiptOnly()
    {
        TestEngineController controller = new();
        List<object> delivered = [];
        List<(string MessageId, string User)> receive = [];
        List<(string MessageId, string User)> read = [];

        bool ok = await PeerFrameDispatcher.Dispatch(
            Encode(controller, new TestFrame { MessageId = "R1", FromUser = "BOB", ReceiveReceiptMessageId = "M1" }), controller, logger,
            m => { delivered.Add(m); return Task.CompletedTask; },
            (id, user) => { read.Add((id, user)); return Task.CompletedTask; },
            (id, user) => { receive.Add((id, user)); return Task.CompletedTask; });

        Assert.True(ok);
        Assert.Empty(delivered);
        Assert.Empty(read);
        Assert.Equal(("M1", "BOB"), Assert.Single(receive));
    }
}
