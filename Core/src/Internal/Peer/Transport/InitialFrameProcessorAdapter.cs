namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Presents a host's <see cref="IInitialFrameProcessor{TFrame}"/> as an <see cref="IInitialProcessor"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="processor">The host's processor.</param>
internal sealed class InitialFrameProcessorAdapter<TFrame>(IInitialFrameProcessor<TFrame> processor) : IInitialProcessor where TFrame : class
{
    /// <inheritdoc />
    public Type ItemType { get; } = typeof(TFrame);

    /// <inheritdoc />
    public Task OnConnected(IInitialSession session) => processor.OnConnected(new Context(session));

    /// <inheritdoc />
    public Task OnInitial(IInitialSession session, object item) => processor.OnInitial(new Context(session), (TFrame)item);

    /// <inheritdoc />
    public Task OnReply(IInitialSession session, object item) => processor.OnReply(new Context(session), (TFrame)item);

    private sealed class Context(IInitialSession session) : IInitialFrameContext<TFrame>
    {
        public bool IsOpener => session.IsOpener;

        public IConnectionInfo Connection => session.Connection;

        public UserInfo CurrentUser => session.Engine.CurrentUser;

        public IEnumerable<UserInfo> Users => session.Engine.Users;

        public IEnumerable<UserInfo> ConnectedUsers => session.Engine.ConnectedUsers;

        public bool IsConnected(string userName) => session.Engine.IsConnected(userName);

        public void Connected(string userName) => session.Connected(userName);

        public void Disconnect() => session.Disconnect();

        public Task<bool> Send(TFrame frame) => session.Send(frame);
    }
}
