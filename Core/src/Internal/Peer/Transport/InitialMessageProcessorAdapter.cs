namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Presents a host's <see cref="IInitialMessageProcessor{TMessage}"/> as an <see cref="IInitialProcessor"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="processor">The host's processor.</param>
internal sealed class InitialMessageProcessorAdapter<TMessage>(IInitialMessageProcessor<TMessage> processor) : IInitialProcessor where TMessage : class
{
    /// <inheritdoc />
    public Type ItemType { get; } = typeof(TMessage);

    /// <inheritdoc />
    public Task OnConnected(IInitialSession session) => processor.OnConnected(new Context(session));

    /// <inheritdoc />
    public Task OnInitial(IInitialSession session, object item) => processor.OnInitial(new Context(session), (TMessage)item);

    /// <inheritdoc />
    public Task OnReply(IInitialSession session, object item) => processor.OnReply(new Context(session), (TMessage)item);

    private sealed class Context(IInitialSession session) : IInitialMessageContext<TMessage>
    {
        public bool IsOpener => session.IsOpener;

        public IConnectionInfo Connection => session.Connection;

        public UserInfo CurrentUser => session.Engine.CurrentUser;

        public IEnumerable<UserInfo> Users => session.Engine.Users;

        public IEnumerable<UserInfo> ConnectedUsers => session.Engine.ConnectedUsers;

        public bool IsConnected(string userName) => session.Engine.IsConnected(userName);

        public void Connected(string userName) => session.Connected(userName);

        public void Disconnect() => session.Disconnect();

        public Task<bool> Send(TMessage message) => session.Send(message);
    }
}
