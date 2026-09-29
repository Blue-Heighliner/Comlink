namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IEngineConfiguration"/> mapping the logical message fields onto <see cref="TestMessage"/>, and optionally the packet fields onto <see cref="TestPacket"/>.</summary>
/// <param name="packets">Whether to turn packetization on with <see cref="TestPacket"/>.</param>
public sealed class TestEngineConfiguration(bool packets = false) : IEngineConfiguration
{
    /// <inheritdoc />
    public IEngineBuilder Configure(IEngineBuilder engine)
    {
        engine.Message<TestMessage>(message => message
            .Id(m => m.MessageId)
            .Sender(m => m.FromUser)
            .Subject(m => m.Subject)
            .Body(m => m.Body)
            .Addresses(
                m => m.Addresses.Select(a => (a.UserName, a.Type.ParseAddressType(), a.Information)),
                (m, value) => m.Addresses = [.. value.Select(a => new TestAddressEntry { UserName = a.Name, Type = a.Type.ToString(), Information = a.Information })])
            .SentAt(m => m.SentAt)
            .ConfirmationId(m => m.ConfirmationMessageId)
            .IsAlert(m => m.IsAlert)
            .Priority(m => m.Priority)
            .Tag(m => m.Tag)
            .SecurityLevel(m => m.SecurityLevel));

        if (packets)
        {
            engine.Packets<TestPacket>(packet => packet
                .PayloadId(p => p.PayloadId)
                .Index(p => p.Index)
                .Count(p => p.Count)
                .PayloadLength(p => p.PayloadLength)
                .Data(p => p.Data, (p, value) => p.Data = value.ToArray()));
        }

        return engine;
    }
}
