namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PeerMessageDispatcher"/>'s classification of received bytes.</summary>
public sealed class PeerMessageDispatcherTests
{
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");

    private static ReadOnlyMemory<byte> Encode(IEngineController controller, TestMessage message)
    {
        using IMemoryOwner<byte> buf = controller.NetworkSerializer.Serialize(message);
        return buf.Memory.ToArray();
    }

    /// <summary>An ordinary message is delivered.</summary>
    [Fact]
    public async Task Dispatch_OrdinaryMessage_IsDelivered()
    {
        TestEngineController controller = new();
        List<object> delivered = [];

        bool ok = await PeerMessageDispatcher.Dispatch(Encode(controller, new TestMessage { MessageId = "M1" }), controller, logger, m => { delivered.Add(m); return Task.CompletedTask; }, null);

        Assert.True(ok);
        Assert.Single(delivered);
    }

    /// <summary>A retrieval request is neither delivered as a message nor treated as a confirmation: only a storage server answers one.</summary>
    [Fact]
    public async Task Dispatch_RetrievalRequest_IsIgnored()
    {
        TestEngineController controller = new();
        List<object> delivered = [];
        int confirmations = 0;

        bool ok = await PeerMessageDispatcher.Dispatch(
            Encode(controller, new TestMessage { MessageId = "R1", IsRetrieval = true }), controller, logger,
            m => { delivered.Add(m); return Task.CompletedTask; }, (_, _) => { confirmations++; return Task.CompletedTask; });

        Assert.True(ok);
        Assert.Empty(delivered);
        Assert.Equal(0, confirmations);
    }
}
