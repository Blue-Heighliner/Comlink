namespace BlueHeighliner.Comlink;

/// <summary>
/// Configures the MSMT settings used for every IP connection, inbound and outbound, including the interface listener, continuing the fluent chain of the engine builder: one call per setting, for example <c>.Msmt().HandshakeTimeout(TimeSpan.FromSeconds(20)).Display&lt;MyDisplayHandler&gt;()</c>. The identity certificate and trusted authorities are not among them; the engine supplies those. Anything not stated keeps the MSMT package's own default.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/>.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> : IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>States how long a TCP connection attempt and its TLS handshake may take before it is abandoned, which also bounds how long an accepted connection may take to send its first message. <see langword="null"/> disables the timeout. Defaults to 30 seconds.</summary>
    /// <param name="handshakeTimeout">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> HandshakeTimeout(TimeSpan? handshakeTimeout);

    /// <summary>States how long a message transfer may make no progress at all, in either direction, before the connection is dropped as stalled. <see langword="null"/> disables the timeout. Defaults to 30 seconds.</summary>
    /// <param name="stallTimeout">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> StallTimeout(TimeSpan? stallTimeout);

    /// <summary>States how long to wait for the acknowledgement of a message sent, measured from when it was fully written, before the connection is dropped and the send fails. <see langword="null"/> disables the timeout. Defaults to 2 minutes.</summary>
    /// <param name="responseTimeout">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ResponseTimeout(TimeSpan? responseTimeout);

    /// <summary>States how long a TCP connection may be silent before the operating system starts probing whether the remote host is still there. <see langword="null"/> leaves TCP keep-alive disabled. Defaults to 1 minute.</summary>
    /// <param name="tcpKeepAliveTime">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> TcpKeepAliveTime(TimeSpan? tcpKeepAliveTime);

    /// <summary>States the cap applied to a connecting client's proposed session lifetime: the agreed lifetime is the lesser of the two. Defaults to 10 minutes.</summary>
    /// <param name="maximumSessionLifetime">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MaximumSessionLifetime(TimeSpan maximumSessionLifetime);

    /// <summary>States the maximum connection lifetime proposed when opening a connection. The remote peer may agree to less. Defaults to 10 minutes.</summary>
    /// <param name="sessionLifetime">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> SessionLifetime(TimeSpan sessionLifetime);

    /// <summary>States the shortest time a connection this node opened may sit without any message before a keep-alive is sent. Defaults to 3 minutes.</summary>
    /// <param name="keepAliveMinInterval">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> KeepAliveMinInterval(TimeSpan keepAliveMinInterval);

    /// <summary>States the longest time a connection this node opened may sit without any message before a keep-alive is sent. Must not be less than the minimum. Defaults to 5 minutes.</summary>
    /// <param name="keepAliveMaxInterval">The value to use.</param>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> KeepAliveMaxInterval(TimeSpan keepAliveMaxInterval);
}
