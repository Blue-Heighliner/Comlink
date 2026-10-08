namespace BlueHeighliner.Comlink;

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
    public TimeSpan Timeout => processor.Timeout;

    /// <inheritdoc />
    public Task OnReceived(IInitialSession session, object item) => processor.OnReceived(new Context(session), (TPacket)item);

    private sealed class Context(IInitialSession session) : IInitialPacketContext<TPacket>
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
