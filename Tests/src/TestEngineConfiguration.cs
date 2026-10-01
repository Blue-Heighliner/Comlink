namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IEngineConfiguration"/> mapping the logical message fields onto <see cref="TestMessage"/>, and optionally the packet fields onto <see cref="TestPacket"/>.</summary>
/// <param name="packets">Whether to turn packetization on with <see cref="TestPacket"/>.</param>
/// <param name="messageExtra">Further message settings to state after the field mapping, such as the network processor.</param>
/// <param name="packetExtra">Further packet settings to state after the field mapping, such as the initial packet processor. Turns packetization on.</param>
public sealed class TestEngineConfiguration(bool packets = false, Action<IMessageBuilder<TestMessage>>? messageExtra = null, Action<IPacketBuilder<TestPacket>>? packetExtra = null) : IEngineConfiguration
{
    /// <inheritdoc />
    public IEngineBuilder Configure(IEngineBuilder engine)
    {
        engine.Message<TestMessage>(message =>
        {
            message
                .Id(m => m.MessageId)
                .Sender(m => m.FromUser)
                .Subject(m => m.Subject)
                .Body(m => m.Body)
                .Addresses(
                    m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType(), a.Information)),
                    (m, value) => m.Addresses = [.. value.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString(), Information = a.Information })])
                .SentAt(m => m.SentAt)
                .ConfirmationId(m => m.ConfirmationMessageId)
                .Retrieval(r => r.IsRequest(m => m.IsRetrieval).From(m => m.RetrievalFrom).To(m => m.RetrievalTo).Authors(m => m.RetrievalAuthors, (m, v) => m.RetrievalAuthors = [.. v]).Destinations(m => m.RetrievalDestinations, (m, v) => m.RetrievalDestinations = [.. v]).Ids(m => m.RetrievalIds, (m, v) => m.RetrievalIds = [.. v]))
                .IsAlert(m => m.IsAlert)
                .Priority(m => m.Priority)
                .Tag(m => m.Tag)
                .SecurityLevel(m => m.SecurityLevel);
            messageExtra?.Invoke(message);
        });

        if (packets || packetExtra is not null)
        {
            engine.Packets<TestPacket>(packet =>
            {
                packet
                    .PayloadId(p => p.PayloadId)
                    .Index(p => p.Index)
                    .Count(p => p.Count)
                    .PayloadLength(p => p.PayloadLength)
                    .Data(p => p.Data, (p, value) => p.Data = value.ToArray());
                packetExtra?.Invoke(packet);
            });
        }

        return engine;
    }
}
