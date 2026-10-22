namespace BlueHeighliner.Comlink.Testing;

/// <summary>
/// A stand-in for the <see cref="IPacketHandshakeContext{TPacket}"/> a packet handshake handler is handed, to test a handshake without a connection. Whatever the handler sends is recorded in <see cref="Sent"/> and
/// accepted unless <see cref="SendResult"/> says otherwise; <see cref="Accept"/> and <see cref="Reject"/> are recorded too and finish at once.
/// </summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <param name="connection">The connection the handshake is on.</param>
public sealed class FakePacketHandshakeContext<TPacket>(IConnectionInfo connection) : IPacketHandshakeContext<TPacket> where TPacket : class
{
    /// <summary>Gets every packet the handler sent, in order.</summary>
    public List<TPacket> Sent { get; } = [];

    /// <summary>Gets the user name of each call to <see cref="Accept"/>, in order.</summary>
    public List<string> Accepted { get; } = [];

    /// <summary>Gets the reason of each call to <see cref="Reject"/>, in order.</summary>
    public List<string> Rejected { get; } = [];

    /// <summary>Gets or sets whether the other node accepts what is sent, which <c>Send</c> reports. Defaults to <see langword="true"/>.</summary>
    public bool SendResult { get; set; } = true;

    /// <inheritdoc />
    public IConnectionInfo Connection { get; set; } = connection;

    /// <inheritdoc />
    public Task Accept(string userName)
    {
        Accepted.Add(userName);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Reject(string reason)
    {
        Rejected.Add(reason);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> Send(TPacket packet)
    {
        Sent.Add(packet);
        return Task.FromResult(SendResult);
    }
}
