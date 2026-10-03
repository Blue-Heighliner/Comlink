namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Demonstrates injecting a custom packet DTO, which enables the engine's standard packetization. As with
/// <see cref="Frame"/>, the field names are deliberately unlike the engine's own logical ones, so it is
/// <see cref="FramePacketHandler"/> that maps them; the engine does all the splitting
/// and reassembling itself.
/// </summary>
[ProtoContract]
public sealed class Packet
{
    /// <summary>Identifier shared by every packet of one payload.</summary>
    [ProtoMember(1)] public int Group { get; set; }
    /// <summary>Zero-based position of the packet among its payload's packets.</summary>
    [ProtoMember(2)] public int Position { get; set; }
    /// <summary>Number of packets the payload was broken into.</summary>
    [ProtoMember(3)] public int Total { get; set; }
    /// <summary>Length in bytes of the whole payload.</summary>
    [ProtoMember(4)] public int FullLength { get; set; }
    /// <summary>The slice of the payload this packet carries.</summary>
    [ProtoMember(5)] public byte[] Chunk { get; set; } = [];
    /// <summary>Whether this packet carries a piece of a frame, as opposed to a packet that carries none, such as an identification packet.</summary>
    [ProtoMember(6)] public bool IsFramePacket { get; set; }
    /// <summary>Whether this packet is a heartbeat, which carries nothing.</summary>
    [ProtoMember(7)] public bool IsHeartbeat { get; set; }
}
