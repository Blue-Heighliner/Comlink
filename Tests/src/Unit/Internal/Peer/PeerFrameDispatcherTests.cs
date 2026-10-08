namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerFrameDispatcher"/>'s turning received bytes into frames.</summary>
public sealed class PeerFrameDispatcherTests
{
    private static ReadOnlyMemory<byte> Encode(IEngineController controller, TestFrame frame)
    {
        using IMemoryOwner<byte> buf = controller.FrameSerializer.Serialize(frame);
        return buf.Memory.ToArray();
    }

    /// <summary>Whatever a peer sends is a frame, so it is raised with the user it arrived from and nothing is made of what is in it.</summary>
    [Fact]
    public async Task Dispatch_AnyFrame_IsRaisedWithItsSource()
    {
        TestEngineController controller = new();
        List<ReceivedFrame> received = [];

        bool ok = await PeerFrameDispatcher.Dispatch(Encode(controller, new TestFrame { MessageId = "M1" }), controller, r => { received.Add(r); return Task.CompletedTask; }, "SERVER");

        Assert.True(ok);
        ReceivedFrame frame = Assert.Single(received);
        Assert.Equal("SERVER", frame.SourceUser);
        Assert.Equal("M1", Assert.IsType<TestFrame>(frame.Frame).MessageId);
    }

    /// <summary>Receipts, requests and frames with no identifier are all raised alike: the engine does not tell them apart.</summary>
    [Fact]
    public async Task Dispatch_ReceiptRequestAndAnonymousFrames_AreRaisedToo()
    {
        TestEngineController controller = new();
        List<ReceivedFrame> received = [];
        Func<ReceivedFrame, Task> raise = r => { received.Add(r); return Task.CompletedTask; };

        await PeerFrameDispatcher.Dispatch(Encode(controller, new TestFrame { ReceiveReceiptMessageId = "M1", IsHidden = true }), controller, raise, "A");
        await PeerFrameDispatcher.Dispatch(Encode(controller, new TestFrame { IsRetrieval = true, IsHidden = true }), controller, raise, "B");
        await PeerFrameDispatcher.Dispatch(Encode(controller, new TestFrame { FromUser = "ALICE" }), controller, raise, "C");

        Assert.Equal(["A", "B", "C"], received.Select(r => r.SourceUser));
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
        List<ReceivedFrame> received = [];

        bool ok = await PeerFrameDispatcher.Dispatch(new byte[] { 1 }, controller.Object, r => { received.Add(r); return Task.CompletedTask; }, "SERVER", packet);

        Assert.True(ok);
        Assert.Same(frame, Assert.Single(received).Frame);
    }

    /// <summary>A heartbeat is acknowledged and not raised, since it only keeps the connection live.</summary>
    [Fact]
    public async Task Dispatch_Heartbeat_IsAcknowledgedAndIgnored()
    {
        TestEngineController controller = new();
        List<ReceivedFrame> received = [];

        bool ok = await PeerFrameDispatcher.Dispatch(TestHeartbeat.Bytes(), controller, r => { received.Add(r); return Task.CompletedTask; }, "SERVER");

        Assert.True(ok);
        Assert.Empty(received);
    }

    /// <summary>An empty payload is not a serialized frame, so it is refused rather than treated as a heartbeat.</summary>
    [Fact]
    public async Task Dispatch_EmptyPayload_IsRefused()
        => Assert.False(await PeerFrameDispatcher.Dispatch(ReadOnlyMemory<byte>.Empty, new TestEngineController(), null, "SERVER"));

    /// <summary>A frame of another type than the configured one, which an incompatible sender's bytes can describe, is refused.</summary>
    [Fact]
    public async Task Dispatch_FrameOfAnotherType_IsRefused()
    {
        Mock<IFrameSerializer> serializer = new();
        serializer.Setup(s => s.Deserialize(It.IsAny<ReadOnlyMemory<byte>>(), null)).Returns(new object());
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.FrameSerializer).Returns(serializer.Object);
        List<ReceivedFrame> received = [];

        Assert.False(await PeerFrameDispatcher.Dispatch(new byte[] { 1 }, controller.Object, r => { received.Add(r); return Task.CompletedTask; }, "SERVER"));
        Assert.Empty(received);
    }
}
