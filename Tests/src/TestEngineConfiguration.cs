namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IEngineConfiguration"/> mapping the logical message fields onto <see cref="TestFrame"/>, and optionally the packet fields onto <see cref="TestPacket"/>.</summary>
/// <param name="packets">Whether to turn packetization on with <see cref="TestPacket"/>.</param>
/// <param name="messageExtra">Further message settings to state after the field mapping, such as the network processor.</param>
/// <param name="packetExtra">Further packet settings to state after the field mapping, such as the initial packet processor. Turns packetization on.</param>
/// <param name="heartbeats">Whether to state the heartbeat handler.</param>
public sealed class TestEngineConfiguration(bool packets = false, Action<TestFrameBuilder>? messageExtra = null, Action<TestPacketBuilder>? packetExtra = null, bool heartbeats = true) : IEngineConfiguration
{
    /// <inheritdoc />
    public void Configure(IEngineBuilder engine) => Apply(engine);

    /// <summary>Fixes the test types on <paramref name="engine"/> and states the handlers, returning the typed builder.</summary>
    /// <param name="engine">The builder to configure.</param>
    public TestEngineBuilder Apply(IEngineBuilder engine)
    {
        TestEngineBuilder typed = engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel>();
        IPriorityBuilder<TestFrame, TestPacket, TestMessagePriority, TestLevel> priorities = typed.Priorities();
        foreach (TestMessagePriority priority in Enum.GetValues<TestMessagePriority>()) { priorities.Priority(priority); }

        ISecurityLevelsBuilder<TestFrame, TestPacket, TestMessagePriority, TestLevel> levels = typed.SecurityLevels();
        foreach (TestLevel level in Enum.GetValues<TestLevel>()) { levels.Level(level); }

        TestFrameBuilder message = typed.Frames()
            .Message<TestMessageHandler>()
            .Retrieval<TestRetrievalHandler>()
            .ReadReceipt<TestReadReceiptHandler>()
            .ReceiveReceipt<TestReceiveReceiptHandler>();

        if (heartbeats) { message.Heartbeat<TestHeartbeatHandler>(); }

        messageExtra?.Invoke(message);

        if (packets || packetExtra is not null)
        {
            TestPacketBuilder packet = typed.Packets().Frame<TestFramePacketHandler>();
            packetExtra?.Invoke(packet);
        }

        return typed;
    }
}
