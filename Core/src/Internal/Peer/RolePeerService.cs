namespace BlueHeighliner.Comlink;

/// <summary>The one <see cref="IPeerService"/> the engine depends on, whose role-specific implementation can be replaced while it runs.</summary>
internal interface IRolePeerService : IPeerService
{
    /// <summary>
    /// Drops every connection and listener of the running implementation and brings them back up from the configuration as it is now, choosing the
    /// implementation for the current role again. Does nothing before <see cref="IPeerService.Start"/>.
    /// </summary>
    void Restart();

    /// <summary>Applies the changes in the configuration to the running implementation without restarting it, touching only what changed. Does nothing before <see cref="IPeerService.Start"/>.</summary>
    void Reconfigure();
}

/// <summary>
/// The <see cref="IPeerService"/> the rest of the engine depends on. Which implementation does the work is decided by
/// <see cref="IEngineController.Role"/>, which comes from the installed user's <see cref="UserInfo"/> and so is not known
/// until a user is installed; this therefore creates the <see cref="ClientPeerService"/>, <see cref="RelayPeerService"/> or
/// <see cref="ServerRoutingService"/> when <see cref="Start"/> runs, and forwards its events. Until then no user is
/// connected and nothing can be sent.
/// </summary>
internal sealed class RolePeerService(IServiceProvider services, IEngineController engineController) : IRolePeerService, IConnectionStatusService, IAsyncDisposable
{
    private readonly Lock innerLock = new();
    private IPeerService? inner;
    private CancellationTokenSource? current;
    private bool restartRequested;

    /// <inheritdoc />
    public event Func<object, Task>? FrameDelivered;

    /// <inheritdoc />
    public event Func<string, string, Task>? ReadReceiptReceived;

    /// <inheritdoc />
    public event Func<string, string, Task>? ReceiveReceiptReceived;

    /// <inheritdoc />
    public event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;

    /// <inheritdoc />
    public event Func<string, Task>? UserConnected;

    /// <inheritdoc />
    public event Func<string, Task>? UserDisconnected;

    /// <inheritdoc />
    public event Action? StatusesChanged;

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedUsers() => inner?.GetConnectedUsers() ?? [];

    /// <inheritdoc />
    public bool IsUserConnected(string userName) => inner?.IsUserConnected(userName) ?? false;

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        while (true)
        {
            using CancellationTokenSource run = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            IPeerService created;
            lock (innerLock)
            {
                if (restartRequested) { restartRequested = false; }
                created = Create();
                current = run;
                inner = created;
            }

            StatusesChanged?.Invoke();
            try { await created.Start(run.Token); }
            catch (OperationCanceledException) when (run.IsCancellationRequested) { }
            catch (InvalidOperationException ex) when (ex is not InvalidEngineConfigurationException)
            {
                ILogger? logger = services.GetService<ILoggerFactory>()?.CreateLogger(LogCategories.App);
                logger?.Record(LogEvents.NetworkingCouldNotStart, "Networking could not start: {Message}", ex.Message);
                logger?.Record(LogEvents.NetworkingNotWorking, "Networking is not working: {Reason}", "its certificates or settings could not be used");
            }

            if (created is IAsyncDisposable disposable) { await disposable.DisposeAsync(); }
            lock (innerLock)
            {
                if (ReferenceEquals(inner, created)) { inner = null; }
                if (!restartRequested || cancellation.IsCancellationRequested) { return; }
            }
        }
    }

    /// <inheritdoc />
    public void Reconfigure() => (inner as IReconfigurable)?.Reconfigure();

    /// <inheritdoc />
    public void Restart()
    {
        lock (innerLock)
        {
            if (current is null) { return; }

            restartRequested = true;
            current.Cancel();
        }
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
        => inner?.Send(userName, message, cancellation) ?? Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
        => inner?.SendPacket(userName, packet, cancellation) ?? Task.FromResult(false);

    /// <inheritdoc />
    public Task DeliverLocal(object payload) => inner?.DeliverLocal(payload) ?? Raise(FrameDelivered, handler => handler(payload));

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses() => (inner as IConnectionStatusService)?.GetStatuses() ?? [];

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed) => (inner as IConnectionStatusService)?.SetClosed(kind, userName, closed);

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName) => (inner as IConnectionStatusService)?.Refresh(kind, userName);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;

    private IPeerService Create()
    {
        IPeerService created = engineController.Role switch
        {
            UserRole.Client => ActivatorUtilities.CreateInstance<ClientPeerService>(services),
            UserRole.Server => ActivatorUtilities.CreateInstance<ServerRoutingService>(services),
            UserRole.Relay => ActivatorUtilities.CreateInstance<RelayPeerService>(services),
            _ => ActivatorUtilities.CreateInstance<ClientPeerService>(services)
        };
        created.FrameDelivered += payload => Raise(FrameDelivered, handler => handler(payload));
        created.ReceiveReceiptReceived += (messageId, userName) => Raise(ReceiveReceiptReceived, handler => handler(messageId, userName));
        created.ReadReceiptReceived += (messageId, userName) => Raise(ReadReceiptReceived, handler => handler(messageId, userName));
        created.DeliveryStatusChanged += (messageId, userName, status) => Raise(DeliveryStatusChanged, handler => handler(messageId, userName, status));
        created.UserConnected += userName => Raise(UserConnected, handler => handler(userName));
        created.UserDisconnected += userName => Raise(UserDisconnected, handler => handler(userName));
        if (created is IConnectionStatusService status) { status.StatusesChanged += () => StatusesChanged?.Invoke(); }
        return created;
    }

    private async Task Raise<THandler>(THandler? handlers, Func<THandler, Task> invoke) where THandler : Delegate
    {
        if (handlers is null) { return; }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            await invoke((THandler)handler);
        }
    }
}
