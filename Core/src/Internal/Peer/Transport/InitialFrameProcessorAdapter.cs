namespace BlueHeighliner.Comlink;

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
    public TimeSpan Timeout => processor.Timeout;

    /// <inheritdoc />
    public Task OnReceived(IInitialSession session, object item) => processor.OnReceived(new Context(session), (TFrame)item);

    private sealed class Context(IInitialSession session) : IInitialFrameContext<TFrame>
    {
        public IConnectionInfo Connection => session.Connection;

        public UserInfo CurrentUser => session.Engine.CurrentUser;

        public IReadOnlyDictionary<string, UserInfo> Users => session.Engine.Users;

        public IReadOnlyDictionary<string, UserInfo> ConnectedUsers => session.Engine.ConnectedUsers;

        public IReadOnlyList<string> GetGroupMembers(string groupName) => session.Engine.GetGroupMembers(groupName);

        public Task Connected(string userName) => session.Connected(userName);

        public Task Disconnect() => session.Disconnect();

        public Task<bool> Send(TFrame frame) => session.Send(frame);
    }
}
