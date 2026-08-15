namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Hosts the local interface listener: an OFT connection that behaves like a peer connection — same
/// transport, same message type (<see cref="IEngineController.MessageType"/>) — but represents no user of
/// its own. Every message this user receives from a peer is mirrored to every connected interface, and
/// every message an interface sends is routed out to other peers as if this user had originated it itself.
/// </summary>
internal interface IInterfaceService : IAsyncDisposable
{
    /// <summary>Starts the inbound interface listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IInterfaceService" />
internal sealed class InterfaceService : IInterfaceService
{
    /// <summary>Initializes a new <see cref="InterfaceService"/> and subscribes to inbound peer deliveries to mirror.</summary>
    public InterfaceService(
        IOftHoster hoster,
        IEngineController engineController,
        IMessageRoutingService routingService,
        IUserService userService,
        IPeerService peerService)
    {
        this.hoster = hoster;
        this.engineController = engineController;
        this.routingService = routingService;
        this.userService = userService;
        peerService.MessageDelivered += OnMessageDelivered;
    }

    private readonly IOftHoster hoster;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService routingService;
    private readonly IUserService userService;

    private readonly ConcurrentDictionary<Guid, IOftConnection> connections = new();

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        IOftListener listener = await hoster.Host(
            new IPEndPoint(IPAddress.Loopback, engineController.InterfacePort),
            new OftConnectionOptions { Info = string.Empty, SecurityMode = OftSecurityMode.Trusted },
            cancellation);
        listener.ConnectedHandler = OnConnected;

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private void OnConnected(IOftConnection connection)
    {
        Guid id = Guid.NewGuid();
        connections[id] = connection;
        connection.DisconnectedHandler = ex => connections.TryRemove(id, out IOftConnection? _);
        connection.ReceivedHandler = OnReceived;
    }

    private void OnReceived(IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleInterfaceMessage(copy));
    }

    internal async Task HandleInterfaceMessage(ReadOnlyMemory<byte> data)
    {
        object? message;
        try
        {
            message = PeerSerializer.Deserialize(engineController.MessageType, data);
        }
        catch
        {
            return;
        }
        if (message is null) { return; }

        UserInfo? userInfo = userService.GetCurrentUserInfo();
        if (userInfo is null) { return; }

        SendMessagePayload payload = new()
        {
            Subject = engineController.GetSubject(message),
            Body = engineController.GetBody(message),
            Addresses = engineController.GetAddresses(message).Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type.ToString() }).ToList(),
            IsAlert = engineController.GetIsAlert(message),
            Priority = engineController.GetPriority(message),
            Tag = engineController.GetTag(message)
        };

        await routingService.Route(userInfo.Name, payload, CancellationToken.None);
    }

    private async Task OnMessageDelivered(object message)
    {
        if (connections.IsEmpty) { return; }

        int priority = engineController.GetPriority(message);
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        foreach (IOftConnection connection in connections.Values)
        {
            try { await connection.Send(buf.Memory, priority); }
            catch { }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (IOftConnection connection in connections.Values)
        {
            await connection.DisposeAsync();
        }
        connections.Clear();
    }
}
