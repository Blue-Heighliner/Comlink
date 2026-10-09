namespace BlueHeighliner.Comlink;

/// <summary>Per-send settings for <see cref="IPeerTransport.Request"/>.</summary>
internal sealed record PeerSendOptions
{
    /// <summary>Send priority; higher values go first on every link, MSMT and HDLC alike.</summary>
    public int Priority { get; init; }

    /// <summary>The frame the payload is the serialization of, which a packetizing transport hands the packet handler and the packet serializer. Required for a payload a packetizing transport splits; not used for a packet sent as it is (<see cref="IsPacket"/>).</summary>
    public object? Frame { get; init; }

    /// <summary>Whether the payload is already a serialized packet, which a packetizing transport sends as it is instead of splitting it.</summary>
    public bool IsPacket { get; init; }
    /// <summary>Invoked once the payload has been handed to the remote end and the send is awaiting its acknowledgement.</summary>
    public Action? Transmitted { get; init; }
}
