namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// The <see cref="IPeerService"/> the rest of the engine depends on. Which implementation does the work is decided by
/// <see cref="IEngineController.Role"/>, which comes from the installed user's <see cref="UserInfo"/> and so is not known
/// until a user is installed; this therefore creates the <see cref="PeerService"/>, <see cref="ClientPeerService"/> or
/// <see cref="ServerRoutingService"/> when <see cref="Start"/> runs, and forwards its events. Until then no user is
/// connected and nothing can be sent.
/// </summary>
internal sealed class RolePeerService(IServiceProvider services, IEngineController engineController) : IPeerService, IConnectionStatusService, IAsyncDisposable
{
    private readonly Lock innerLock = new();
    private IPeerService? inner;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;

    /// <inheritdoc />
    public event Func<string, string, Task>? ConfirmationReceived;

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
    public Task Start(CancellationToken cancellation)
    {
        IPeerService created;
        lock (innerLock)
        {
            if (inner is not null) { throw new InvalidOperationException("The peer service is already started"); }

            created = engineController.Role switch
            {
                UserRole.Client => ActivatorUtilities.CreateInstance<ClientPeerService>(services),
                UserRole.Server => ActivatorUtilities.CreateInstance<ServerRoutingService>(services),
                _ => ActivatorUtilities.CreateInstance<PeerService>(services)
            };
            created.MessageDelivered += payload => Raise(MessageDelivered, handler => handler(payload));
            created.ConfirmationReceived += (messageId, userName) => Raise(ConfirmationReceived, handler => handler(messageId, userName));
            created.DeliveryStatusChanged += (messageId, userName, status) => Raise(DeliveryStatusChanged, handler => handler(messageId, userName, status));
            created.UserConnected += userName => Raise(UserConnected, handler => handler(userName));
            created.UserDisconnected += userName => Raise(UserDisconnected, handler => handler(userName));
            if (created is IConnectionStatusService status) { status.StatusesChanged += () => StatusesChanged?.Invoke(); }
            inner = created;
        }

        StatusesChanged?.Invoke();
        return created.Start(cancellation);
    }

    /// <inheritdoc />
    public Task<bool> Send(string userName, object message, CancellationToken cancellation = default)
        => inner?.Send(userName, message, cancellation) ?? Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default)
        => inner?.SendPacket(userName, packet, cancellation) ?? Task.FromResult(false);

    /// <inheritdoc />
    public Task DeliverLocal(object payload) => inner?.DeliverLocal(payload) ?? Raise(MessageDelivered, handler => handler(payload));

    /// <inheritdoc />
    public IReadOnlyList<PeerConnectionStatus> GetStatuses() => (inner as IConnectionStatusService)?.GetStatuses() ?? [];

    /// <inheritdoc />
    public void SetClosed(PeerConnectionKind kind, string userName, bool closed) => (inner as IConnectionStatusService)?.SetClosed(kind, userName, closed);

    /// <inheritdoc />
    public void Refresh(PeerConnectionKind kind, string userName) => (inner as IConnectionStatusService)?.Refresh(kind, userName);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner is IAsyncDisposable disposable ? disposable.DisposeAsync() : ValueTask.CompletedTask;

    private async Task Raise<THandler>(THandler? handlers, Func<THandler, Task> invoke) where THandler : Delegate
    {
        if (handlers is null) { return; }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            await invoke((THandler)handler);
        }
    }
}
