namespace BlueHeighliner.Comlink;

/// <summary>
/// The MSMT settings a host can state for every IP connection, inbound and outbound, including the interface listener (see <see cref="IConnectionsBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Msmt(MsmtConnectionOptions)"/>). The identity
/// certificate and trusted authorities are not among them; the engine supplies those. Each default is the MSMT package's own.
/// </summary>
public sealed record MsmtConnectionOptions
{
    /// <summary>Gets how long a TCP connection attempt and its TLS handshake may take before it is abandoned. Also bounds how long an accepted connection may take to send its first message. <see langword="null"/> disables the timeout.</summary>
    public TimeSpan? HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long a message transfer may make no progress at all, in either direction, before the connection is dropped as stalled. <see langword="null"/> disables the timeout.</summary>
    public TimeSpan? StallTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long to wait for the acknowledgement of a message sent, measured from when it was fully written, before the connection is dropped and the send fails. <see langword="null"/> disables the timeout.</summary>
    public TimeSpan? ResponseTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Gets how long a TCP connection may be silent before the operating system starts probing whether the remote host is still there. <see langword="null"/> leaves TCP keep-alive disabled.</summary>
    public TimeSpan? TcpKeepAliveTime { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the cap applied to a connecting client's proposed session lifetime: the agreed lifetime is the lesser of the two.</summary>
    public TimeSpan MaximumSessionLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets the maximum connection lifetime proposed when opening a connection. The remote peer may agree to less.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Gets the shortest time a connection this node opened may sit without any message before a keep-alive is sent.</summary>
    public TimeSpan KeepAliveMinInterval { get; init; } = TimeSpan.FromMinutes(3);

    /// <summary>Gets the longest time a connection this node opened may sit without any message before a keep-alive is sent. Must not be less than <see cref="KeepAliveMinInterval"/>.</summary>
    public TimeSpan KeepAliveMaxInterval { get; init; } = TimeSpan.FromMinutes(5);
}
