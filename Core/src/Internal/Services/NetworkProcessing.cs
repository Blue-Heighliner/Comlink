namespace BlueHeighliner.Comlink;

/// <summary>Runs the host's network processor (<see cref="IEngineController.NetworkHandler"/>) when something happens that it takes part in. Each event gets a freshly built context, so the processor sees a consistent snapshot even if a user connects or disconnects while it runs, and runs in the background: a failure is logged, never thrown back.</summary>
internal interface INetworkProcessing
{
    /// <summary>A user went from having no live connection to having one.</summary>
    /// <param name="userName">The user.</param>
    void Connected(string userName);

    /// <summary>A user went from having a live connection to having none.</summary>
    /// <param name="userName">The user.</param>
    void Disconnected(string userName);

    /// <summary>A frame was received.</summary>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <param name="origin">Where it came from.</param>
    /// <param name="sourceUser">The user it arrived from, or the name of the external system.</param>
    void Received(object frame, FrameOrigin origin, string sourceUser);

    /// <summary>The user sent a message with the GUI, which is stored.</summary>
    /// <param name="message">The message.</param>
    void Sent(Message message);

    /// <summary>The user opened a message they received.</summary>
    /// <param name="message">The message.</param>
    void Read(Message message);

    /// <summary>The user submitted a retrieval with the GUI.</summary>
    /// <param name="server">The server asked.</param>
    /// <param name="criteria">What the stored messages must fit.</param>
    /// <returns><see langword="true"/> when a processor was there to be told, <see langword="false"/> when none is stated.</returns>
    bool Retrieval(string server, RetrievalCriteria criteria);
}

/// <inheritdoc cref="INetworkProcessing" />
internal sealed class NetworkProcessing(IEngineController engineController, INetworkEnvironment environment, ILoggerFactory loggerFactory) : INetworkProcessing
{
    private readonly ILogger logger = loggerFactory.CreateLogger(LogCategories.App);

    /// <inheritdoc />
    public void Connected(string userName) => Run(handler => handler.OnConnected(environment, userName), "OnConnected", userName);

    /// <inheritdoc />
    public void Disconnected(string userName) => Run(handler => handler.OnDisconnected(environment, userName), "OnDisconnected", userName);

    /// <inheritdoc />
    public void Received(object frame, FrameOrigin origin, string sourceUser) => Run(handler => handler.OnReceived(environment, frame, origin, sourceUser), "OnReceived", sourceUser, origin is FrameOrigin.ExternalSystem ? null : frame);

    /// <inheritdoc />
    public void Sent(Message message) => Run(handler => handler.OnSent(environment, message), "OnSent", message.Id);

    /// <inheritdoc />
    public void Read(Message message) => Run(handler => handler.OnRead(environment, message), "OnRead", message.Id);

    /// <inheritdoc />
    public bool Retrieval(string server, RetrievalCriteria criteria)
    {
        if (engineController.NetworkHandler is null)
        {
            return false;
        }

        Run(handler => handler.OnRetrieval(environment, server, criteria), "OnRetrieval", server);
        return true;
    }

    private void Run(Func<INetworkHandler, Task> run, string name, string subject, object? unhandled = null)
    {
        if (engineController.NetworkHandler is not { } handler)
        {
            unhandled.TryDispose();
            return;
        }

        _ = Task.Run(async () =>
        {
            try { await run(handler); }
            catch (Exception ex) { logger.Record(LogEvents.NetworkProcessorFailed, ex, "The network processor's {Name} failed for {Subject}", name, subject); }
        });
    }
}
