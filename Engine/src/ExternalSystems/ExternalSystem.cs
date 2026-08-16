namespace BlueHeighliner.Comlink.Engine.ExternalSystems;

/// <summary>
/// A conduit between this system and one external system outside Comlink — not generic over the message
/// type, so <see cref="ExternalSystemsService"/> can hold and drive every configured external system (each
/// an <see cref="ExternalSystemBase{TMessage}"/> closed over the host's own message type) uniformly. See
/// <c>Docs/ExternalSystems.md</c>.
/// </summary>
public interface IExternalSystem
{
    /// <summary>
    /// Raised whenever a message (an instance of <see cref="Control.IEngineController.MessageType"/>) is
    /// received from the external system while connected.
    /// </summary>
    event Func<object, Task>? MessageReceived;

    /// <summary>A short, human-readable name identifying this external system (e.g. for logging).</summary>
    string Name { get; }
    /// <summary>Whether a connection to the external system is currently established.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Starts the connect/poll/disconnect lifecycle: periodically attempts to (re)connect while
    /// disconnected, and periodically polls to detect the external system going away while connected.
    /// Blocks until <paramref name="cancellation"/> is cancelled.
    /// </summary>
    Task Start(CancellationToken cancellation);
    /// <summary>
    /// Sends a message (an instance of <see cref="Control.IEngineController.MessageType"/>) to the external
    /// system.
    /// </summary>
    /// <returns><see langword="true"/> if the message was sent successfully; <see langword="false"/> if not currently connected, or the send failed.</returns>
    Task<bool> Send(object message);
    /// <summary>
    /// Assigns the logger this external system uses to report connection lifecycle events. Called once by
    /// <see cref="ExternalSystemsService"/>, using its own <see cref="ILoggerFactory"/>, before <see cref="Start"/>
    /// — an external system is constructed directly by a host's <see cref="Control.IEngineController.GetExternalSystems"/>
    /// implementation rather than resolved through DI, so it cannot safely take <see cref="ILoggerFactory"/>
    /// as a constructor dependency itself (doing so on the same type backing <see cref="Control.IEngineController"/>
    /// would create a circular dependency through the logging providers that themselves depend on
    /// <see cref="Control.IEngineController"/> for their log file location).
    /// </summary>
    /// <param name="logger">The logger to use for connection lifecycle events from this point on.</param>
    void AttachLogger(ILogger logger);
}

/// <summary>
/// Base class for an external system integration, generic over the host's concrete message type
/// <typeparamref name="TMessage"/> — matching <see cref="Control.DefaultEngineController{TMessage}"/>'s own
/// type parameter, since <see cref="Control.DefaultEngineController{TMessage}.GetExternalSystems"/> returns
/// a list of these. Handles the connect/poll/disconnect lifecycle so a derived class only needs to supply
/// the real connection behavior for its specific external system, via three abstract methods —
/// <see cref="TryConnect"/> (attempt to establish a connection), <see cref="Disconnect"/> (release a
/// connection once it is known to be gone), and <see cref="Send(TMessage)"/> (send one message over an
/// established connection) — plus one optional virtual method, <see cref="PollIsConnected"/> (check whether an
/// established connection is still alive; the default always reports it is, for an implementation that
/// instead learns about disconnection through an event or callback and calls <see cref="ReportDisconnected"/>
/// when that happens). A derived class calls <see cref="Receive"/> whenever its connection delivers
/// an inbound message — concurrently and without awaiting each call before making the next one, if its own
/// connection can genuinely deliver messages that way (e.g. parallel socket reads); <see cref="Receive"/>
/// only enqueues the message; a single internal loop delivers queued messages to <see cref="MessageReceived"/>
/// one at a time, in enqueue order, for the lifetime of <see cref="Start"/>. See <c>Docs/ExternalSystems.md</c>
/// and <c>Sample/src/SampleExternalSystem.cs</c> for a worked example.
/// </summary>
/// <typeparam name="TMessage">The host's concrete message type — the same type argument supplied to <see cref="Control.DefaultEngineController{TMessage}"/>.</typeparam>
/// <param name="name">A short, human-readable name identifying this external system.</param>
/// <param name="connectRetryInterval">How long to wait between connection attempts while disconnected; defaults to 5 seconds. Overriding this is intended for unit testing.</param>
/// <param name="pollInterval">How long to wait between connection-status polls while connected; defaults to 5 seconds. Overriding this is intended for unit testing.</param>
public abstract class ExternalSystemBase<TMessage>(string name, TimeSpan? connectRetryInterval = null, TimeSpan? pollInterval = null) : IExternalSystem where TMessage : class
{
    private ILogger logger = NullLogger.Instance;
    private readonly TimeSpan connectRetryInterval = connectRetryInterval ?? TimeSpan.FromSeconds(5);
    private readonly TimeSpan pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
    private readonly Channel<TMessage> receivedMessages = Channel.CreateUnbounded<TMessage>();
    private CancellationTokenSource? disconnectSignal;

    /// <inheritdoc />
    public event Func<object, Task>? MessageReceived;

    /// <inheritdoc />
    public string Name { get; } = name;
    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        Task deliveryLoop = DeliverReceivedMessages();
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (!IsConnected)
                {
                    bool connected;
                    try { connected = await TryConnect(cancellation); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "External system {Name} failed to connect", Name);
                        connected = false;
                    }

                    if (connected)
                    {
                        IsConnected = true;
                        disconnectSignal = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                        logger.LogInformation("External system {Name} connected", Name);
                    }
                    else
                    {
                        await Task.Delay(connectRetryInterval, cancellation);
                        continue;
                    }
                }

                bool stillConnected;
                try
                {
                    await Task.Delay(pollInterval, disconnectSignal!.Token);
                    stillConnected = await PollIsConnected(cancellation);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    // disconnectSignal was cancelled via ReportDisconnected — the connection is already known
                    // to be lost, so there is nothing to poll for.
                    stillConnected = false;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "External system {Name} failed to poll connection status", Name);
                    stillConnected = false;
                }

                if (!stillConnected)
                {
                    IsConnected = false;
                    disconnectSignal?.Dispose();
                    disconnectSignal = null;
                    logger.LogInformation("External system {Name} disconnected", Name);
                    try { await Disconnect(); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "External system {Name} failed to release its connection cleanly", Name);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            disconnectSignal?.Dispose();
            receivedMessages.Writer.TryComplete();
            await deliveryLoop;
        }
    }

    /// <inheritdoc />
    public async Task<bool> Send(object message)
    {
        if (!IsConnected) { return false; }
        try { return await Send((TMessage)message); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "External system {Name} failed to send a message", Name);
            return false;
        }
    }

    /// <inheritdoc />
    public void AttachLogger(ILogger logger) => this.logger = logger;

    /// <summary>
    /// Called by a derived class whenever its connection delivers an inbound message while connected. Only
    /// enqueues <paramref name="message"/> for delivery — safe to call concurrently, or without awaiting a
    /// previous call first, since actual delivery to <see cref="MessageReceived"/> always happens one
    /// message at a time, in enqueue order, on a single internal loop running for the lifetime of
    /// <see cref="Start"/>. A message enqueued before <see cref="Start"/> has been called, or after it has
    /// returned, is silently dropped, since there is no running delivery loop to hand it to.
    /// </summary>
    /// <param name="message">The received message, in this instance's own <typeparamref name="TMessage"/>.</param>
    protected async Task Receive(TMessage message)
    {
        try { await receivedMessages.Writer.WriteAsync(message); }
        catch (ChannelClosedException)
        {
            logger.LogWarning("External system {Name} received a message while not running; dropping it", Name);
        }
    }

    /// <summary>
    /// Called by a derived class to immediately report that its established connection has been lost, for
    /// an implementation that learns about disconnection through an event or callback rather than
    /// <see cref="PollIsConnected"/> polling. Interrupts the current poll wait (if any), so the
    /// disconnect/reconnect cycle reacts right away instead of waiting up to the poll interval. A no-op if
    /// not currently connected.
    /// </summary>
    protected void ReportDisconnected()
    {
        try { disconnectSignal?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private async Task DeliverReceivedMessages()
    {
        await foreach (TMessage message in receivedMessages.Reader.ReadAllAsync())
        {
            if (MessageReceived is null) { continue; }
            try { await MessageReceived(message); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "External system {Name} failed to process a received message", Name);
            }
        }
    }

    /// <summary>Attempts to establish a connection to the external system. Called repeatedly, on the configured retry interval, while disconnected.</summary>
    /// <param name="cancellation">Cancellation token, signaled when the whole external-system lifecycle is stopping.</param>
    /// <returns><see langword="true"/> if the connection was established.</returns>
    protected abstract Task<bool> TryConnect(CancellationToken cancellation);
    /// <summary>
    /// Checks whether a previously established connection is still alive. Called repeatedly, on the
    /// configured poll interval, while connected. The default implementation always returns
    /// <see langword="true"/> — override only if the external system requires active polling to detect a
    /// dropped connection; an implementation that instead learns about disconnection through an event or
    /// callback should call <see cref="ReportDisconnected"/> when that happens, and can leave this at its
    /// default.
    /// </summary>
    /// <param name="cancellation">Cancellation token, signaled when the whole external-system lifecycle is stopping.</param>
    /// <returns><see langword="true"/> if the connection is still alive.</returns>
    protected virtual Task<bool> PollIsConnected(CancellationToken cancellation) => Task.FromResult(true);
    /// <summary>Releases a connection after it is known to be gone, whether detected by <see cref="PollIsConnected"/> or reported via <see cref="ReportDisconnected"/>.</summary>
    protected abstract Task Disconnect();
    /// <summary>Sends a single message over the established connection.</summary>
    /// <param name="message">The message to send, in this instance's own <typeparamref name="TMessage"/>.</param>
    /// <returns><see langword="true"/> if the message was sent successfully.</returns>
    protected abstract Task<bool> Send(TMessage message);
}
