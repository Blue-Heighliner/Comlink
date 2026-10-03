namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// Wraps another <see cref="IPeerTransport"/> so that a connection is only published, and only usable, once its initial exchange has completed and,
/// optionally, the node on the other end has been identified. The exchange is a <see cref="Handshake"/>: a host's processor is told when the connection
/// forms and is given each item that arrives, on the accepting node as an initial item and on the opening node (the node at the higher station address
/// for a serial link) as a reply, until it marks the connection connected as a named user or disconnects it. Each item is an instance of the frame or
/// packet type serialized with its own serializer and nothing is added to it, so items are recognized by position: every payload a node receives on a
/// connection that is still in its exchange is an item, and everything after the connection is marked connected is ordinary data. The exchange has to
/// complete within a timeout or the connection is dropped. With <c>identify</c>, the user the processor named (here or in the exchange beneath this one),
/// else <see cref="IEngineController.IdentifyConnection"/>, else the engine's own rule (a certificate name matching a user, or the user named on the serial
/// point or else the serial port name) decides who is on the other end, and the result is set on the connection's <see cref="PeerConnection.User"/>; a
/// connection that cannot be identified is dropped.
/// </summary>
internal sealed class HandshakePeerTransport : IPeerTransport
{
    private const int MaxBufferedPayloads = 64;

    /// <summary>Initializes a new <see cref="HandshakePeerTransport"/> over <paramref name="inner"/>.</summary>
    /// <param name="inner">The transport to wrap.</param>
    /// <param name="engineController">Identifies connections.</param>
    /// <param name="logger">Receives a warning for every connection that is refused.</param>
    /// <param name="handshake">The initial exchange to carry out, or <see langword="null"/> for none.</param>
    /// <param name="identify">Whether to identify the node on the other end once the exchange is done. Only the outermost handshake transport does.</param>
    /// <param name="handshakeTimeout">How long the exchange may take. Defaults to ten seconds.</param>
    /// <param name="contexts">Creates the engine snapshot a processor sees, or <see langword="null"/> for one that knows no connected users.</param>
    public HandshakePeerTransport(IPeerTransport inner, IEngineController engineController, ILogger logger, Handshake? handshake, bool identify, TimeSpan? handshakeTimeout = null, IEngineContextFactory? contexts = null)
    {
        this.inner = inner;
        this.engineController = engineController;
        this.logger = logger;
        this.handshake = handshake;
        this.identify = identify;
        this.handshakeTimeout = handshakeTimeout ?? TimeSpan.FromSeconds(10);
        this.contexts = contexts;

        inner.Received.Listen(OnReceived);
        inner.Connected.Listen(OnConnected);
        inner.Disconnected.Listen(OnDisconnected);
    }

    private readonly IPeerTransport inner;
    private readonly IEngineController engineController;
    private readonly ILogger logger;
    private readonly Handshake? handshake;
    private readonly bool identify;
    private readonly TimeSpan handshakeTimeout;
    private readonly IEngineContextFactory? contexts;
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
    public void StopListener() => inner.StopListener();

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
        return await inner.Request(connection, data, options, cancellation);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    // The node that opens a connection starts the exchange. A serial link has no opener, since both ends open the port, so the node at the higher station
    // address plays that part and the other accepts.
    private static bool IsInitiator(ConnectionInfo info) => !info.IsInbound && (info is not SerialConnectionInfo serial || serial.SerialAddress > serial.RemoteSerialAddress);

    private Session GetSession(PeerConnection connection)
        => sessions.GetValue(connection, key =>
        {
            Session created = new(key);
            created.Initial = new InitialSession(this, created);
            return created;
        });

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

        if (handshake is null)
        {
            Establish(session);
            return;
        }

        RunExchange(session);
    }

    private void RunExchange(Session session)
    {
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(handshakeTimeout, session.Deadline.Token); }
            catch (OperationCanceledException) { return; }

            Fail(session, "did not complete its initial exchange in time");
        });

        Process(session, () => handshake!.Processor.OnConnected(session.Initial));
    }

    private void Process(Session session, Action work)
    {
        lock (session.Gate) { session.Tail = Chain(session, session.Tail, work); }
    }

    // What a processor asks of its session (send, connected, disconnect) runs after the handler that asked, in the order asked, and still runs once an
    // earlier one has marked the connection connected, unlike the handlers themselves, which only run during the exchange.
    private void Enqueue(Session session, Func<Task> action)
    {
        lock (session.Gate) { session.Tail = Run(session, session.Tail, action); }
    }

    private async Task Run(Session session, Task previous, Func<Task> action)
    {
        await previous;
        if (session.State == SessionState.Closed) { return; }

        try { await action(); }
        catch (Exception ex) { Fail(session, $"could not complete its initial exchange: {ex.Message}"); }
    }

    private async Task Chain(Session session, Task previous, Action work)
    {
        await previous;
        await Task.Yield();
        if (session.State != SessionState.Handshaking) { return; }

        try { work(); }
        catch (Exception ex) { Fail(session, $"could not complete its initial exchange: {ex.Message}"); }
    }

    private async Task<bool> Send(Session session, object item)
    {
        using IMemoryOwner<byte> body = handshake!.Serialize(item);
        return await inner.Request(session.Connection, body.Memory, new PeerSendOptions { Priority = int.MaxValue, Frame = handshake.CarriesFrames ? item : null }, session.Aborted.Token);
    }

    private void OnReceived(PeerReceivedEventArgs args)
    {
        Session session = GetSession(args.Connection);
        Start(session);

        bool isHandshakePayload;
        lock (session.Gate) { isHandshakePayload = handshake is not null && session.State == SessionState.Handshaking; }

        if (isHandshakePayload)
        {
            byte[] body = args.Payload.ToArray();
            object? packet = args.Packet;
            Process(session, () => OnHandshakePayload(session, body, packet));
            return;
        }

        OnData(session, args);
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

    private void OnHandshakePayload(Session session, byte[] body, object? packet)
    {
        object item = handshake!.Deserialize(body, packet);
        if (item.GetType() != handshake.Processor.ItemType) { throw new InvalidDataException($"expected a {handshake.Processor.ItemType.Name}"); }

        if (IsInitiator(session.Connection.Info)) { handshake.Processor.OnReply(session.Initial, item); }
        else { handshake.Processor.OnInitial(session.Initial, item); }
    }

    private void Establish(Session session)
    {
        PeerConnection connection = session.Connection;
        lock (session.Gate)
        {
            if (session.State != SessionState.Handshaking) { return; }
            session.State = SessionState.Establishing;
        }

        if (identify)
        {
            UserIdentity? identity;
            try { identity = Identify(connection); }
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
            }
        }

        lock (session.Gate) { session.IsPublished = true; }
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
        session.Deadline.Cancel();
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
        session.Deadline.Cancel();
        session.Aborted.Cancel();
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

        args.Connection.InitialUser = null;
        session.Established.TrySetResult(false);
        session.Deadline.Cancel();
        session.Aborted.Cancel();
        if (wasPublished) { disconnected.Publish(args); }
    }

    private UserIdentity? Identify(PeerConnection connection)
    {
        ConnectionInfo info = connection.Info;
        string? name = connection.InitialUser ?? engineController.IdentifyConnection(info) ?? info switch { SerialConnectionInfo serial => SerialUser(serial), IpConnectionInfo ip => MatchCertificate(ip.CertificateNames), _ => null };
        return name is null ? null : new UserIdentity { Name = name, Data = engineController.GetUserData(name) };
    }

    private string? SerialUser(SerialConnectionInfo info)
        => engineController.OutgoingPoints.FirstOrDefault(point => point.IsSerial && point.User is not null
            && string.Equals(point.SerialPort, info.SerialPort, StringComparison.OrdinalIgnoreCase) && point.SerialAddress == info.SerialAddress)?.User
            ?? info.SerialPort;

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
            names.UnionWith(config.Children);
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
        public CancellationTokenSource Deadline { get; } = new();
        public CancellationTokenSource Aborted { get; } = new();
        public TaskCompletionSource<bool> Established { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<PeerReceivedEventArgs> Pending { get; set; } = [];
        public SessionState State { get; set; }
        public bool IsStarted { get; set; }
        public bool IsPublished { get; set; }
        public Task Tail { get; set; } = Task.CompletedTask;
        public IInitialSession Initial { get; set; } = null!;
    }

    private sealed class InitialSession(HandshakePeerTransport owner, Session session) : IInitialSession
    {
        public bool IsOpener => IsInitiator(session.Connection.Info);

        public IConnectionInfo Connection => owner.engineController.WithLocalUser(session.Connection.Info);

        public IEngineContext Engine => owner.contexts?.Create() ?? new EngineContext(new UserInfo { Name = Connection.LocalUser ?? string.Empty }, owner.engineController.Users, owner.engineController.GetUserInfo, _ => false);

        public void Connected(string userName)
            => owner.Enqueue(session, () =>
            {
                session.Connection.InitialUser = userName;
                owner.Establish(session);
                return Task.CompletedTask;
            });

        public void Disconnect()
            => owner.Enqueue(session, () =>
            {
                owner.Fail(session, "was disconnected by its initial processor");
                return Task.CompletedTask;
            });

        public void Send(object item)
            => owner.Enqueue(session, async () =>
            {
                if (!await owner.Send(session, item)) { throw new IOException("an initial item was not accepted for sending"); }
            });
    }
}
