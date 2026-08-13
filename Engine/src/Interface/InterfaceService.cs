namespace BlueHeighliner.Comlink.Engine.Interface;

/// <summary>
/// Hosts the local interface listener: an OFT connection that behaves like a peer connection — same
/// transport, same message type (<see cref="IMessageFormat.MessageType"/>) — but represents no user of
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
    private readonly IOftHoster _hoster;
    private readonly IPortConfiguration _ports;
    private readonly IMessageRoutingService _routingService;
    private readonly IUserService _userService;
    private readonly IMessageFormat _messageFormat;
    private readonly ConcurrentDictionary<Guid, IOftConnection> _connections = new();

    /// <summary>Initializes a new <see cref="InterfaceService"/> and subscribes to inbound peer deliveries to mirror.</summary>
    public InterfaceService(
        IOftHoster hoster,
        IPortConfiguration ports,
        IMessageRoutingService routingService,
        IUserService userService,
        IMessageFormat messageFormat,
        IPeerService peerService)
    {
        _hoster = hoster;
        _ports = ports;
        _routingService = routingService;
        _userService = userService;
        _messageFormat = messageFormat;
        peerService.MessageDelivered += OnMessageDelivered;
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        IOftListener listener = await _hoster.Host(
            new IPEndPoint(IPAddress.Loopback, _ports.InterfacePort),
            new OftConnectionOptions { Info = string.Empty, SecurityMode = OftSecurityMode.Trusted },
            cancellation);
        listener.ConnectedHandler = OnConnected;

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    private void OnConnected(IOftConnection connection)
    {
        Guid id = Guid.NewGuid();
        _connections[id] = connection;
        connection.DisconnectedHandler = ex => _connections.TryRemove(id, out IOftConnection? _);
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
            message = PeerSerializer.Deserialize(_messageFormat.MessageType, data);
        }
        catch
        {
            return;
        }
        if (message is null) return;

        UserInfo? userInfo = _userService.GetCurrentUserInfo();
        if (userInfo is null) return;

        SendMessagePayload payload = new()
        {
            Subject = _messageFormat.GetSubject(message),
            Body = _messageFormat.GetBody(message),
            Addresses = _messageFormat.GetAddresses(message).Select(a => new AddressPayload { UserName = a.UserName, Type = a.Type.ToString() }).ToList(),
            IsAlert = _messageFormat.GetIsAlert(message),
            Priority = _messageFormat.GetPriority(message),
            Tag = _messageFormat.GetTag(message)
        };

        await _routingService.Route(userInfo.Name, payload, CancellationToken.None);
    }

    private async Task OnMessageDelivered(object message)
    {
        if (_connections.IsEmpty) return;

        int priority = _messageFormat.GetPriority(message);
        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        foreach (IOftConnection connection in _connections.Values)
        {
            try { await connection.Send(buf.Memory, priority); }
            catch { }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (IOftConnection connection in _connections.Values)
            await connection.DisposeAsync();
        _connections.Clear();
    }
}
