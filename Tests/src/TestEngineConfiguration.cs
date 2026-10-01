namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IEngineConfiguration"/> mapping the logical message fields onto <see cref="TestFrame"/>, and optionally the packet fields onto <see cref="TestPacket"/>.</summary>
/// <param name="packets">Whether to turn packetization on with <see cref="TestPacket"/>.</param>
/// <param name="messageExtra">Further message settings to state after the field mapping, such as the network processor.</param>
/// <param name="packetExtra">Further packet settings to state after the field mapping, such as the initial packet processor. Turns packetization on.</param>
public sealed class TestEngineConfiguration(bool packets = false, Action<IFrameBuilder<TestFrame>>? messageExtra = null, Action<IPacketBuilder<TestPacket>>? packetExtra = null) : IEngineConfiguration
{
    /// <inheritdoc />
    public IEngineBuilder Configure(IEngineBuilder engine)
    {
        engine.Frames<TestFrame>(message =>
        {
            message
                .Id(m => m.MessageId)
                .Sender(m => m.FromUser)
                .Addresses(
                    m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType(), a.Information)),
                    (m, value) => m.Addresses = [.. value.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString(), Information = a.Information })])
                .Message<TestMessageHandler>()
                .Retrieval<TestRetrievalHandler>()
                .ReadReceipt<TestReadReceiptHandler>()
                .ReceiveReceipt<TestReceiveReceiptHandler>();

            messageExtra?.Invoke(message);
        });

        if (packets || packetExtra is not null)
        {
            engine.Packets<TestPacket>(packet =>
            {
                packet.Frame<TestFramePacketHandler>();
                packetExtra?.Invoke(packet);
            });
        }

        return engine;
    }
}
