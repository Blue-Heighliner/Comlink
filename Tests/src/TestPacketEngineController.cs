namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test packet DTO standing in for a host-supplied <see cref="IEngineController.PacketType"/>.</summary>
[ProtoContract]
public sealed class TestPacket
{
    /// <summary>Identifier shared by every packet of one payload.</summary>
    [ProtoMember(1)] public int PayloadId { get; set; }
    /// <summary>Zero-based position of the packet among its payload's packets.</summary>
    [ProtoMember(2)] public int Index { get; set; }
    /// <summary>Number of packets the payload was broken into.</summary>
    [ProtoMember(3)] public int Count { get; set; }
    /// <summary>Length in bytes of the whole payload.</summary>
    [ProtoMember(4)] public int PayloadLength { get; set; }
    /// <summary>The slice of the payload this packet carries.</summary>
    [ProtoMember(5)] public byte[] Data { get; set; } = [];
}

/// <summary>
/// Test <see cref="IEngineController"/> that, unlike <see cref="TestEngineController"/>, also enables packetization
/// through <see cref="TestPacket"/>. Not <see langword="sealed"/> and <see langword="public"/> for the same reasons
/// as <see cref="TestEngineController"/>: Moq subclasses it with <c>CallBase = true</c>.
/// </summary>
public class TestPacketEngineController() : DefaultEngineController<TestMessage, TestPacket>(new CurrentUserProvider())
{
    /// <inheritdoc />
    protected override string GetMessageId(TestMessage message) => message.MessageId;
    /// <inheritdoc />
    protected override void SetMessageId(TestMessage message, string value) => message.MessageId = value;
    /// <inheritdoc />
    protected override string GetFromUser(TestMessage message) => message.FromUser;
    /// <inheritdoc />
    protected override void SetFromUser(TestMessage message, string value) => message.FromUser = value;
    /// <inheritdoc />
    protected override string GetSubject(TestMessage message) => message.Subject;
    /// <inheritdoc />
    protected override void SetSubject(TestMessage message, string value) => message.Subject = value;
    /// <inheritdoc />
    protected override string GetBody(TestMessage message) => message.Body;
    /// <inheritdoc />
    protected override void SetBody(TestMessage message, string value) => message.Body = value;

    /// <inheritdoc />
    protected override List<MessageAddress> GetAddresses(TestMessage message)
        => message.Addresses
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() })
            .ToList();

    /// <inheritdoc />
    protected override void SetAddresses(TestMessage message, List<MessageAddress> value)
        => message.Addresses = value
            .Select(a => new TestAddressEntry { UserName = a.UserName, Type = a.Type.ToString() })
            .ToList();

    /// <inheritdoc />
    protected override DateTime GetSentAt(TestMessage message) => message.SentAt;
    /// <inheritdoc />
    protected override void SetSentAt(TestMessage message, DateTime value) => message.SentAt = value;
    /// <inheritdoc />
    protected override string GetConfirmationMessageId(TestMessage message) => message.ConfirmationMessageId;
    /// <inheritdoc />
    protected override void SetConfirmationMessageId(TestMessage message, string value) => message.ConfirmationMessageId = value;
    /// <inheritdoc />
    protected override bool GetIsAlert(TestMessage message) => message.IsAlert;
    /// <inheritdoc />
    protected override void SetIsAlert(TestMessage message, bool value) => message.IsAlert = value;
    /// <inheritdoc />
    protected override int GetPriority(TestMessage message) => message.Priority;
    /// <inheritdoc />
    protected override void SetPriority(TestMessage message, int value) => message.Priority = value;
    /// <inheritdoc />
    protected override string GetTag(TestMessage message) => message.Tag;
    /// <inheritdoc />
    protected override void SetTag(TestMessage message, string value) => message.Tag = value;

    /// <inheritdoc />
    protected override int GetPayloadId(TestPacket packet) => packet.PayloadId;
    /// <inheritdoc />
    protected override void SetPayloadId(TestPacket packet, int value) => packet.PayloadId = value;
    /// <inheritdoc />
    protected override int GetPacketIndex(TestPacket packet) => packet.Index;
    /// <inheritdoc />
    protected override void SetPacketIndex(TestPacket packet, int value) => packet.Index = value;
    /// <inheritdoc />
    protected override int GetPacketCount(TestPacket packet) => packet.Count;
    /// <inheritdoc />
    protected override void SetPacketCount(TestPacket packet, int value) => packet.Count = value;
    /// <inheritdoc />
    protected override int GetPayloadLength(TestPacket packet) => packet.PayloadLength;
    /// <inheritdoc />
    protected override void SetPayloadLength(TestPacket packet, int value) => packet.PayloadLength = value;
    /// <inheritdoc />
    protected override ReadOnlyMemory<byte> GetPacketData(TestPacket packet) => packet.Data;
    /// <inheritdoc />
    protected override void SetPacketData(TestPacket packet, ReadOnlyMemory<byte> value) => packet.Data = value.ToArray();
}
