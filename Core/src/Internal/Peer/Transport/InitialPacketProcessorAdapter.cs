namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Presents a host's <see cref="IInitialPacketProcessor{TPacket}"/> as an <see cref="IInitialProcessor"/>.</summary>
/// <typeparam name="TPacket">The host's packet type.</typeparam>
/// <param name="processor">The host's processor.</param>
internal sealed class InitialPacketProcessorAdapter<TPacket>(IInitialPacketProcessor<TPacket> processor) : IInitialProcessor where TPacket : class
{
    /// <inheritdoc />
    public Type ItemType { get; } = typeof(TPacket);

    /// <inheritdoc />
    public Task OnConnected(IInitialSession session) => processor.OnConnected(new Context(session));

    /// <inheritdoc />
    public Task OnInitial(IInitialSession session, object item) => processor.OnInitial(new Context(session), (TPacket)item);

    /// <inheritdoc />
    public Task OnReply(IInitialSession session, object item) => processor.OnReply(new Context(session), (TPacket)item);

    private sealed class Context(IInitialSession session) : IInitialPacketContext<TPacket>
    {
        public bool IsOpener => session.IsOpener;

        public IConnectionInfo Connection => session.Connection;

        public UserInfo CurrentUser => session.Engine.CurrentUser;

        public IEnumerable<UserInfo> Users => session.Engine.Users;

        public IEnumerable<UserInfo> ConnectedUsers => session.Engine.ConnectedUsers;

        public bool IsConnected(string userName) => session.Engine.IsConnected(userName);

        public void Connected(string userName) => session.Connected(userName);

        public void Disconnect() => session.Disconnect();

        public Task<bool> Send(TPacket packet) => session.Send(packet);
    }
}
