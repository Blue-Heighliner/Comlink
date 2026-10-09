namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IEngineConfiguration"/> mapping the logical message fields onto <see cref="TestFrame"/>, and optionally the packet fields onto <see cref="TestPacket"/>.</summary>
/// <param name="packets">Whether to turn packetization on with <see cref="TestPacket"/>.</param>
/// <param name="messageExtra">Further message settings to state after the field mapping, such as the frame handler.</param>
/// <param name="packetExtra">Further packet settings to state after the field mapping, such as the initial packet handler. Turns packetization on.</param>
/// <param name="heartbeats">Whether to state the heartbeat handler.</param>
public sealed class TestEngineConfiguration(bool packets = false, Action<TestFrameBuilder>? messageExtra = null, Action<TestPacketBuilder>? packetExtra = null, bool heartbeats = true) : IEngineConfiguration
{
    /// <inheritdoc />
    public void Configure(IEngineBuilder engine) => Apply(engine);

    /// <summary>Fixes the test types on <paramref name="engine"/> and states the handlers, returning the typed builder.</summary>
    /// <param name="engine">The builder to configure.</param>
    public TestEngineBuilder Apply(IEngineBuilder engine)
    {
        TestEngineBuilder typed = engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>();
        foreach (TestMessagePriority priority in Enum.GetValues<TestMessagePriority>())
        {
            typed.Priority(priority);
        }

        foreach (TestLevel level in Enum.GetValues<TestLevel>())
        {
            typed.Level(level);
        }

        TestFrameBuilder message = typed.Frames<TestFrameHandler>();

        if (heartbeats)
        {
            message.Heartbeat<TestHeartbeatHandler>();
        }

        messageExtra?.Invoke(message);

        if (packets || packetExtra is not null)
        {
            TestPacketBuilder packet = typed.Packets<TestPacketHandler>(16 * 1024);
            packetExtra?.Invoke(packet);
        }

        return typed;
    }
}
