namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>Manages inbound and outbound peer connections and exposes Engine-level delivery events.</summary>
internal interface IPeerService
{
    /// <summary>Raised when a remote site delivers a new message to this site.</summary>
    event Func<object, Task>? MessageDelivered;
    /// <summary>Raised whenever the OFT delivery status of a message sent to a specific site changes.</summary>
    event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;
    /// <summary>Starts the inbound peer listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
    /// <summary>Sends <paramref name="message"/> (an instance of <see cref="IMessageFormat.MessageType"/>) to the peer identified by <paramref name="siteName"/>.</summary>
    Task<bool> Send(string siteName, object message, CancellationToken cancellation = default);
    /// <summary>Raises <see cref="MessageDelivered"/> directly with <paramref name="payload"/>, without a network round-trip. Used when a site sends a message to itself.</summary>
    Task DeliverLocal(object payload);
}

/// <summary>
/// Implements <see cref="IPeerService"/> by wrapping an <see cref="IOftPeer"/>. Traffic carries an
/// instance of <see cref="IMessageFormat.MessageType"/> directly with no envelope; delivery confirmation
/// is derived entirely from OFT's own <see cref="OftDeliveryStatus"/> stream, not from an
/// application-level acknowledgement.
/// </summary>
internal sealed class PeerService : IPeerService, IAsyncDisposable
{
    private readonly IOftPeer _peer;
    private readonly ISiteLocator _siteLocator;
    private readonly IPortConfiguration _ports;
    private readonly IMessageFormat _messageFormat;
    private readonly ILogger _logger;

    /// <inheritdoc />
    public event Func<object, Task>? MessageDelivered;
    /// <inheritdoc />
    public event Func<string, string, OftDeliveryStatus, Task>? DeliveryStatusChanged;

    /// <summary>Initializes a new <see cref="PeerService"/> and wires up an <see cref="IOftPeer"/> using Engine infrastructure.</summary>
    public PeerService(
        IOftPeerFactory peerFactory,
        IPortConfiguration ports,
        ISiteLocator siteLocator,
        IOftCertificateProvider certProvider,
        IMessageFormat messageFormat,
        ILoggerFactory loggerFactory)
    {
        _siteLocator = siteLocator;
        _ports = ports;
        _messageFormat = messageFormat;
        _logger = loggerFactory.CreateLogger("ACTIVITY");
        _peer = peerFactory.Create(certProvider.GetPeerOptions());
        _peer.ReceivedHandler = OnReceived;
        _peer.DeliveryStatusHandler = OnDeliveryStatus;
    }

    /// <summary>Initializes a <see cref="PeerService"/> with a pre-built peer; intended for unit testing.</summary>
    internal PeerService(IOftPeer peer, ISiteLocator siteLocator, IPortConfiguration ports, IMessageFormat messageFormat, ILoggerFactory loggerFactory)
    {
        _peer = peer;
        _siteLocator = siteLocator;
        _ports = ports;
        _messageFormat = messageFormat;
        _logger = loggerFactory.CreateLogger("ACTIVITY");
        _peer.ReceivedHandler = OnReceived;
        _peer.DeliveryStatusHandler = OnDeliveryStatus;
    }

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        await _peer.Listen(new IPEndPoint(IPAddress.Any, _ports.PeerPort), cancellation);
        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
    }

    /// <inheritdoc />
    public async Task<bool> Send(string siteName, object message, CancellationToken cancellation = default)
    {
        SiteEndpoint? endpoint = await _siteLocator.GetEndpoint(siteName, cancellation);
        if (endpoint is null) return false;

        using OwnedBuffer buf = PeerSerializer.Serialize(message);
        try
        {
            await _peer.Send(endpoint.IpAddress, endpoint.Port, buf.Memory, tag: new DeliveryTag(_messageFormat.GetMessageId(message), siteName), cancellationToken: cancellation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task DeliverLocal(object payload)
    {
        _logger.LogInformation("{MessageId} delivered locally from {FromSite}", _messageFormat.GetMessageId(payload), _messageFormat.GetFromSite(payload));
        if (MessageDelivered is not null)
            await MessageDelivered(payload);
    }

    private void OnReceived(OftIdentity identity, IMemoryOwner<byte> data)
    {
        byte[] copy;
        using (data) { copy = data.Memory.ToArray(); }
        _ = Task.Run(() => HandleMessage(copy));
    }

    internal async Task<bool> HandleMessage(ReadOnlyMemory<byte> data)
    {
        try
        {
            object? message = PeerSerializer.Deserialize(_messageFormat.MessageType, data);
            if (message is null) return false;

            _logger.LogInformation("{MessageId} received from {FromSite}", _messageFormat.GetMessageId(message), _messageFormat.GetFromSite(message));
            if (MessageDelivered is not null)
                await MessageDelivered(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnDeliveryStatus(object tag, OftDeliveryStatus status)
    {
        if (tag is not DeliveryTag deliveryTag || DeliveryStatusChanged is null) return;
        _ = Task.Run(() => DeliveryStatusChanged(deliveryTag.MessageId, deliveryTag.SiteName, status));
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _peer.DisposeAsync();

    private sealed record DeliveryTag(string MessageId, string SiteName);
}
