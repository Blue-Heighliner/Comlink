namespace BlueHeighliner.Comlink;

/// <summary>Presents a host's <see cref="IPacketHandshakeHandler{TPacket}"/> as an <see cref="IHandshakeHandler"/>.</summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <param name="handler">The host's handler.</param>
internal sealed class PacketHandshakeHandlerAdapter<TPacket>(IPacketHandshakeHandler<TPacket> handler) : IHandshakeHandler where TPacket : class
{
    /// <inheritdoc />
    public Type ItemType { get; } = typeof(TPacket);

    /// <inheritdoc />
    public Task OnConnected(IHandshakeSession session) => handler.OnConnected(new Context(session));

    /// <inheritdoc />
    public TimeSpan Timeout => handler.Timeout;

    /// <inheritdoc />
    public Task OnReceived(IHandshakeSession session, object item) => handler.OnReceived(new Context(session), (TPacket)item);

    private sealed class Context(IHandshakeSession session) : IPacketHandshakeContext<TPacket>
    {
        public IConnectionInfo Connection => session.Connection;

        public UserInfo CurrentUser => session.Engine.CurrentUser;

        public IReadOnlyDictionary<string, UserInfo> Users => session.Engine.Users;

        public IReadOnlyDictionary<string, UserInfo> ConnectedUsers => session.Engine.ConnectedUsers;

        public IReadOnlyList<string> GetGroupMembers(string groupName) => session.Engine.GetGroupMembers(groupName);

        public Task Connected(string userName) => session.Connected(userName);

        public Task Disconnect() => session.Disconnect();

        public Task<bool> Send(TPacket packet) => session.Send(packet);
    }
}
