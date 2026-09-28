namespace BlueHeighliner.Comlink.Peer;

/// <summary>Distinguishes the two kinds of connection a <see cref="PeerConnectionStatus"/> can describe, so the UI can group them into separate tables.</summary>
internal enum PeerConnectionKind
{
    /// <summary>A connection to another server: a Server's outbound link to another server in the cluster, or a Client's single link to its server.</summary>
    Server,
    /// <summary>A Server's inbound connection from one of its own configured child clients.</summary>
    Client
}

/// <summary>Point-in-time status of one configured peer connection: one of a Server's own child clients, a Server's outbound link to another server in the cluster, or a Client's single link to its server.</summary>
internal sealed record PeerConnectionStatus
{
    /// <summary>The remote user name this connection is (or was) established with.</summary>
    public required string UserName { get; init; }
    /// <summary>Whether this is a connection to another server or to a child client — see <see cref="PeerConnectionKind"/>.</summary>
    public required PeerConnectionKind Kind { get; init; }
    /// <summary>Whether the connection is currently established.</summary>
    public required bool IsConnected { get; init; }
    /// <summary>When the connection was last established, or <see langword="null"/> if never.</summary>
    public DateTime? LastConnectedAt { get; init; }
    /// <summary>When the connection was last lost, or <see langword="null"/> if it has never disconnected.</summary>
    public DateTime? LastDisconnectedAt { get; init; }
    /// <summary>Whether the connection has been closed by the user: it stays down, is not re-formed, and connections from that user are rejected, until it is reopened.</summary>
    public bool IsClosed { get; init; }
}

/// <summary>
/// Exposes live connection status for <see cref="ViewModels.IConnectionStatusViewModel"/> — registered only
/// for <see cref="NodeRole.Client"/> (the single connection to its server) and <see cref="NodeRole.Server"/>
/// (one entry per own child client, plus one entry per other server in the cluster); <see cref="NodeRole.Peer"/>
/// registers <see cref="NullConnectionStatusService"/> instead, since peer-to-peer connections are not
/// configured, long-term links. Implemented directly by <see cref="ClientPeerService"/>/<see cref="ServerRoutingService"/>
/// rather than a separate tracking component, since they already own the connection state this reports on.
/// </summary>
internal interface IConnectionStatusService
{
    /// <summary>Raised whenever any tracked connection's status, last-connected time, or last-disconnected time changes.</summary>
    event Action? StatusesChanged;

    /// <summary>Returns the current status of every tracked connection.</summary>
    IReadOnlyList<PeerConnectionStatus> GetStatuses();

    /// <summary>
    /// Closes or reopens one tracked connection. Closing drops it, stops trying to re-form it, and rejects connections
    /// from that user; reopening lets it re-form. The state lasts until reopened or the application restarts.
    /// </summary>
    /// <param name="kind">Which table the connection is in.</param>
    /// <param name="userName">The remote user name of the connection (empty for a client's single server connection).</param>
    /// <param name="closed"><see langword="true"/> to close, <see langword="false"/> to reopen.</param>
    void SetClosed(PeerConnectionKind kind, string userName, bool closed);

    /// <summary>Drops one tracked connection and re-forms it straight away. Has no effect on a closed connection.</summary>
    /// <param name="kind">Which table the connection is in.</param>
    /// <param name="userName">The remote user name of the connection (empty for a client's single server connection).</param>
    void Refresh(PeerConnectionKind kind, string userName);
}

/// <summary>Default <see cref="IConnectionStatusService"/> for <see cref="NodeRole.Peer"/>, where no configured connections are tracked.</summary>
internal sealed class NullConnectionStatusService : IConnectionStatusService
{
    /// <inheritdoc />
    public event Action? StatusesChanged { add { } remove { } }

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses() => [];

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed)
    {
    }

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName)
    {
    }
}
