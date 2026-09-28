namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Published for every message received on any connection. The message is acknowledged to the sender once every subscriber has returned.</summary>
internal sealed record PeerReceivedEventArgs
{
    /// <summary>The connection the message arrived on.</summary>
    public required PeerConnection Connection { get; init; }

    /// <summary>The complete message payload, which the subscriber may keep.</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
