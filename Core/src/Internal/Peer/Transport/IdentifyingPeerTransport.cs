namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Wraps another <see cref="IPeerTransport"/> so that a connection is only published, and only usable, once the node on
/// the other end has been identified. When the engine controller configures a connection message
/// (<see cref="IEngineController.ConnectionMessageType"/>) the node that opened the connection sends one first (both ends
/// of a serial link do), the node that receives it may answer with a response, and every payload from then on travels
/// in a one byte frame that tells the three apart; the exchange has to complete, within a timeout, before the connection
/// counts. Without one there is no framing and a connection is identified the moment it forms. Either way
/// <see cref="IEngineController.IdentifyConnection"/> decides who is on the other end, falling back to the engine's own
/// rule (a certificate name matching a user, or the serial port name), and the result is set on the connection's
/// <see cref="PeerConnection.User"/>. A connection that cannot be identified is dropped. An empty payload is a
/// <see cref="PeerConnectionMonitor"/> heartbeat and is framed like any other data.
/// </summary>
internal sealed class IdentifyingPeerTransport : IPeerTransport
{
    private const byte DataFrame = 1;
    private const byte MessageFrame = 2;
    private const byte ResponseFrame = 3;
    private const int MaxBufferedPayloads = 64;

    /// <summary>Initializes a new <see cref="IdentifyingPeerTransport"/> over <paramref name="inner"/>.</summary>
    /// <param name="inner">The transport to wrap.</param>
    /// <param name="engineController">Decides the connection message configuration and identifies connections.</param>
    /// <param name="logger">Receives a warning for every connection that is refused.</param>
    /// <param name="handshakeTimeout">How long the connection message exchange may take. Defaults to ten seconds.</param>
    public IdentifyingPeerTransport(IPeerTransport inner, IEngineController engineController, ILogger logger, TimeSpan? handshakeTimeout = null)
    {
        this.inner = inner;
        this.engineController = engineController;
        this.logger = logger;
        this.handshakeTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(10);
        if (engineController.ConnectionMessageType is not null)
        {
            serializer = engineController.ConnectionSerializer ?? throw new InvalidOperationException("A connection message type needs a connection serializer, but the engine controller has none");
            respond = engineController.ConnectionResponseType is not null;
        }

        inner.Received.Listen(OnReceived);
        inner.Connected.Listen(OnConnected);
        inner.Disconnected.Listen(OnDisconnected);
    }

    private readonly IPeerTransport inner;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly TimeSpan handshakeTimeout;
    private readonly INetworkSerializer? serializer;
    private readonly bool respond;
    private readonly ConditionalWeakTable<PeerConnection, Session> sessions = new();
    private readonly PeerEvent<PeerReceivedEventArgs> received = new();
    private readonly PeerEvent<PeerConnectionEventArgs> connected = new();
    private readonly PeerEvent<PeerConnectionEventArgs> disconnected = new();

    /// <inheritdoc />
    public IObservable<PeerReceivedEventArgs> Received => received;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Connected => connected;

    /// <inheritdoc />
    public IObservable<PeerConnectionEventArgs> Disconnected => disconnected;

    /// <inheritdoc />
    public void StartListener(int port) => inner.StartListener(port);

    /// <inheritdoc />
    public void SetClosed(ConnectionPoint point, bool closed) => inner.SetClosed(point, closed);

    /// <inheritdoc />
    public void Reset(ConnectionPoint point) => inner.Reset(point);

    /// <inheritdoc />
    public async Task<PeerConnection> Connect(ConnectionPoint point, CancellationToken cancellation = default)
    {
        PeerConnection connection = await inner.Connect(point, cancellation);
        await WaitUntilEstablished(connection, cancellation);
        return connection;
    }

    /// <inheritdoc />
    public async Task<bool> Request(PeerConnection connection, ReadOnlyMemory<byte> data, PeerSendOptions? options = null, CancellationToken cancellation = default)
    {
        await WaitUntilEstablished(connection, cancellation);
        return await inner.Request(connection, serializer is null ? data : Frame(DataFrame, data), options, cancellation);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static byte[] Frame(byte kind, ReadOnlyMemory<byte> body)
    {
        byte[] frame = new byte[1 + body.Length];
        frame[0] = kind;
        body.Span.CopyTo(frame.AsSpan(1));
        return frame;
    }

    private Session GetSession(PeerConnection connection) => sessions.GetValue(connection, static key => new Session(key));

    private async Task WaitUntilEstablished(PeerConnection connection, CancellationToken cancellation)
    {
        Session session = GetSession(connection);
        Start(session);
        if (!await session.Established.Task.WaitAsync(cancellation)) { throw new IOException("The connection was not accepted"); }
    }

    private void OnConnected(PeerConnectionEventArgs args)
    {
        Session session = GetSession(args.Connection);
        if (session.State == SessionState.Closed)
        {
            // A serial link presents the same connection object again each time it comes back up.
            sessions.Remove(args.Connection);
            session = GetSession(args.Connection);
        }

        Start(session);
    }

    private void Start(Session session)
    {
        lock (session.Gate)
        {
            if (session.IsStarted) { return; }
            session.IsStarted = true;
        }

        if (serializer is null)
        {
            Establish(session);
            return;
        }

        _ = Task.Run(() => RunExchange(session));
    }

    private async Task RunExchange(Session session)
    {
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(handshakeTimeout, session.Lifetime.Token); }
            catch (OperationCanceledException) { return; }

            Fail(session, "did not complete the connection message exchange in time");
        });

        if (session.Connection.IsInbound) { return; }

        try
        {
            object? message = engineController.CreateConnectionMessage(session.Connection.Info);
            if (!await Send(session, MessageFrame, message)) { Fail(session, "had its connection message rejected"); return; }
            Evaluate(session);
        }
        catch (Exception ex)
        {
            Fail(session, $"could not send its connection message: {ex.Message}");
        }
    }

    private async Task<bool> Send(Session session, byte kind, object? value)
    {
        byte[] frame;
        if (value is null)
        {
            frame = Frame(kind, ReadOnlyMemory<byte>.Empty);
        }
        else
        {
            using IMemoryOwner<byte> body = serializer!.Serialize(value);
            frame = Frame(kind, body.Memory);
        }

        return await inner.Request(session.Connection, frame, new PeerSendOptions { Priority = int.MaxValue }, session.Lifetime.Token);
    }

    private void OnReceived(PeerReceivedEventArgs args)
    {
        Session session = GetSession(args.Connection);
        if (serializer is null)
        {
            Start(session);
            OnData(session, args);
            return;
        }

        if (args.Payload.IsEmpty) { return; }

        byte kind = args.Payload.Span[0];
        ReadOnlyMemory<byte> body = args.Payload[1..];
        switch (kind)
        {
            case DataFrame:
                if (session.IsStarted) { OnData(session, new PeerReceivedEventArgs { Connection = args.Connection, Payload = body }); }
                break;
            case MessageFrame:
                _ = Task.Run(() => OnConnectionMessage(session, body));
                break;
            case ResponseFrame:
                OnConnectionResponse(session, body);
                break;
        }
    }

    private void OnData(Session session, PeerReceivedEventArgs args)
    {
        lock (session.Gate)
        {
            if (session.State == SessionState.Closed) { return; }
            if (session.State != SessionState.Open)
            {
                if (session.Pending.Count < MaxBufferedPayloads) { session.Pending.Add(args); }
                return;
            }
        }

        received.Publish(args);
    }

    private async Task OnConnectionMessage(Session session, ReadOnlyMemory<byte> body)
    {
        try
        {
            object? message = Deserialize(body, engineController.ConnectionMessageType!);
            lock (session.Gate)
            {
                session.HasMessage = true;
                session.Connection.Info = session.Connection.Info with { ConnectionMessage = message };
            }

            if (respond)
            {
                object? response = engineController.CreateConnectionResponse(session.Connection.Info);
                if (!await Send(session, ResponseFrame, response)) { Fail(session, "had its connection response rejected"); return; }
            }

            Evaluate(session);
        }
        catch (Exception ex)
        {
            Fail(session, $"sent an unusable connection message: {ex.Message}");
        }
    }

    private void OnConnectionResponse(Session session, ReadOnlyMemory<byte> body)
    {
        try
        {
            object? response = respond ? Deserialize(body, engineController.ConnectionResponseType!) : null;
            lock (session.Gate)
            {
                session.HasResponse = true;
                session.Connection.Info = session.Connection.Info with { ConnectionResponse = response };
            }

            Evaluate(session);
        }
        catch (Exception ex)
        {
            Fail(session, $"sent an unusable connection response: {ex.Message}");
        }
    }

    private object? Deserialize(ReadOnlyMemory<byte> body, Type expected)
    {
        if (body.IsEmpty) { return null; }

        object? value = serializer!.Deserialize(body);
        return value is null || value.GetType() != expected ? throw new InvalidDataException($"expected a {expected.Name}") : value;
    }

    private void Evaluate(Session session)
    {
        PeerConnection connection = session.Connection;
        bool expectMessage = connection.IsInbound || connection.IsSerial;
        bool expectResponse = respond && !connection.IsInbound;
        lock (session.Gate)
        {
            if (session.State != SessionState.Handshaking || (expectMessage && !session.HasMessage) || (expectResponse && !session.HasResponse)) { return; }
        }

        Establish(session);
    }

    private void Establish(Session session)
    {
        PeerConnection connection = session.Connection;
        lock (session.Gate)
        {
            if (session.State != SessionState.Handshaking) { return; }
            session.State = SessionState.Establishing;
        }

        UserIdentity? identity;
        try { identity = Identify(connection.Info); }
        catch (Exception ex)
        {
            Fail(session, $"could not be identified: {ex.Message}");
            return;
        }

        if (identity is null)
        {
            Fail(session, "could not be identified");
            return;
        }

        lock (session.Gate)
        {
            if (session.State == SessionState.Closed) { return; }
            connection.User = identity;
            session.IsPublished = true;
        }

        connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        List<PeerReceivedEventArgs> pending;
        lock (session.Gate)
        {
            pending = session.Pending;
            session.Pending = [];
            if (session.State != SessionState.Closed) { session.State = SessionState.Open; }
        }

        foreach (PeerReceivedEventArgs buffered in pending) { received.Publish(buffered); }
        session.Established.TrySetResult(true);
        session.Lifetime.Cancel();
    }

    private void Fail(Session session, string reason)
    {
        lock (session.Gate)
        {
            if (session.State == SessionState.Closed) { return; }
            session.State = SessionState.Closed;
            session.Pending.Clear();
        }

        logger.LogWarning("Dropped a connection that {Reason}", reason);
        session.Established.TrySetResult(false);
        session.Lifetime.Cancel();
        session.Connection.Drop();
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (!sessions.TryGetValue(args.Connection, out Session? session)) { return; }

        bool wasPublished;
        lock (session.Gate)
        {
            wasPublished = session.IsPublished;
            session.State = SessionState.Closed;
            session.Pending.Clear();
        }

        args.Connection.Info = args.Connection.Info with { ConnectionMessage = null, ConnectionResponse = null };
        session.Established.TrySetResult(false);
        session.Lifetime.Cancel();
        if (wasPublished) { disconnected.Publish(args); }
    }

    private UserIdentity? Identify(ConnectionInfo info)
    {
        UserIdentity? identity = engineController.IdentifyConnection(info);
        if (identity is not null) { return identity; }

        string? name = info.IsSerial ? info.SerialPort : MatchCertificate(info.CertificateNames);
        return name is null ? null : new UserIdentity { Name = name, Data = engineController.GetUserData(name) };
    }

    private string? MatchCertificate(IReadOnlyList<string> certificateNames)
    {
        foreach (string user in KnownUsers())
        {
            string expected = engineController.GetCertificateName(user);
            if (certificateNames.Contains(expected, StringComparer.OrdinalIgnoreCase)) { return user; }
        }

        return certificateNames.FirstOrDefault();
    }

    private IEnumerable<string> KnownUsers()
    {
        HashSet<string> names = new(engineController.Users, StringComparer.OrdinalIgnoreCase);
        foreach ((string server, ServerUserConfig config) in engineController.Servers)
        {
            names.Add(server);
            names.UnionWith(config.ChildClients);
        }

        return names;
    }

    private enum SessionState
    {
        Handshaking,
        Establishing,
        Open,
        Closed
    }

    private sealed class Session(PeerConnection connection)
    {
        public PeerConnection Connection { get; } = connection;
        public Lock Gate { get; } = new();
        public CancellationTokenSource Lifetime { get; } = new();
        public TaskCompletionSource<bool> Established { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<PeerReceivedEventArgs> Pending { get; set; } = [];
        public SessionState State { get; set; }
        public bool IsStarted { get; set; }
        public bool IsPublished { get; set; }
        public bool HasMessage { get; set; }
        public bool HasResponse { get; set; }
    }
}
