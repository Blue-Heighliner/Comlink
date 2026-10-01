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
    /// <summary>Whether the packet carries a piece of a frame.</summary>
    [ProtoMember(6)] public bool IsData { get; set; }
}

/// <summary>
/// Test <see cref="IEngineController"/> that, unlike <see cref="TestEngineController"/>, also enables packetization
/// through <see cref="TestPacket"/>. Not <see langword="sealed"/> for the same reasons as <see cref="TestEngineController"/>.
/// </summary>
internal class TestPacketEngineController() : EngineController(EngineBuilder.Build(new TestEngineConfiguration(packets: true)), new CurrentUserProvider())
{
}
