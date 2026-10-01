namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>The implementation behind <see cref="IConnectionInfo"/>, copied with updated values as the initial exchange fills it in.</summary>
internal abstract record ConnectionInfo : IConnectionInfo
{
    /// <inheritdoc />
    public bool IsInbound { get; init; }

    /// <inheritdoc />
    public string? LocalUser { get; init; }
}
