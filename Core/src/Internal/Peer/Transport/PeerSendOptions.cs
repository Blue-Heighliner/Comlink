namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Per-send settings for <see cref="IPeerTransport.Request"/>.</summary>
internal sealed record PeerSendOptions
{
    /// <summary>Send priority; higher values go first where the transport can honor it. Serial links send in call order and ignore it.</summary>
    public int Priority { get; init; }

    /// <summary>Invoked once the payload has been handed to the remote end and the send is awaiting its acknowledgement.</summary>
    public Action? Transmitted { get; init; }
}
