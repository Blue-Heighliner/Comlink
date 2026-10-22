namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A stand-in for the <see cref="IPacketHandshakeContext{TPacket}"/> a packet handshake handler is handed, to test a handshake without a connection. Whatever the handler sends is recorded in <see cref="Sent"/> and
/// accepted unless <see cref="SendResult"/> says otherwise; <see cref="Connected"/> and <see cref="Disconnect"/> are recorded too and finish at once.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <param name="connection">The connection the handshake is on.</param>
public sealed class FakePacketHandshakeContext<TPacket>(IConnectionInfo connection) : IPacketHandshakeContext<TPacket> where TPacket : class
{
    /// <summary>Gets every packet the handler sent, in order.</summary>
    public List<TPacket> Sent { get; } = [];

    /// <summary>Gets the user name of each call to <see cref="Connected"/>, in order.</summary>
    public List<string> ConnectedAs { get; } = [];

    /// <summary>Gets how many times the handler disconnected.</summary>
    public int DisconnectCount { get; private set; }

    /// <summary>Gets or sets whether the other node accepts what is sent, which <c>Send</c> reports. Defaults to <see langword="true"/>.</summary>
    public bool SendResult { get; set; } = true;

    /// <inheritdoc />
    public IConnectionInfo Connection { get; set; } = connection;

    /// <inheritdoc />
    public Task Connected(string userName)
    {
        ConnectedAs.Add(userName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Disconnect()
    {
        DisconnectCount++;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Send(TPacket packet)
    {
        Sent.Add(packet);
        return Task.FromResult(SendResult);
    }
}
