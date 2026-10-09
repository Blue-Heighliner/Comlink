namespace BlueHeighliner.Comlink;

/// <summary>
/// Wraps another <see cref="IPeerTransport"/> so that a connection is only published, and only usable, once its handshake has completed and,
/// optionally, the node on the other end has been identified. The handshake is a <see cref="Handshake"/>: a host's processor is told when the connection
/// forms and is given each item that arrives, on the accepting node as an initial item and on the opening node (the node at the higher station address
/// for a serial link) as a reply, until it marks the connection connected as a named user or disconnects it. Each item is an instance of the frame or
/// packet type serialized with its own serializer and nothing is added to it, so items are recognized by position: every payload a node receives on a
/// connection that is still in its handshake is an item, and everything after the connection is marked connected is ordinary data. The handshake has to
/// complete within a timeout or the connection is dropped. With <c>identify</c>, the user the processor named (here or in the handshake beneath this one),
/// else the engine's own rule (a certificate name matching a user, or the user named on the serial
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
    /// <param name="handshake">The handshake to carry out, or <see langword="null"/> for none.</param>
    /// <param name="identify">Whether to identify the node on the other end once the handshake is done. Only the outermost handshake transport does.</param>
    /// <param name="contexts">Creates the engine snapshot a processor sees, or <see langword="null"/> for one that knows no connected users.</param>
    public HandshakePeerTransport(IPeerTransport inner, IEngineController engineController, ILogger logger, Handshake? handshake, bool identify, IEngineContextFactory? contexts = null)
    {
        this.inner = inner;
        this.engineController = engineController;
        this.logger = logger;
        this.handshake = handshake;
        this.identify = identify;
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
        if (!await session.Established.Task.WaitAsync(cancellation))
        {
            throw new IOException("The connection was not accepted");
        }
    }

    private void OnConnected(PeerConnectionEventArgs args)
    {
        Session session = GetSession(args.Connection);
        if (session.State is SessionState.Closed)
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
            if (session.IsStarted)
            {
                return;
            }
            session.IsStarted = true;
            if (handshake is not null)
            {
                RunHandshake(session);
                return;
            }
        }

        Establish(session);
    }

    private void RunHandshake(Session session)
    {
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(handshake!.Processor.Timeout, session.Deadline.Token); }
            catch (OperationCanceledException) { return; }

            Fail(session, "did not complete its handshake in time");
        });

        Process(session, () => handshake!.Processor.OnConnected(session.Initial));
    }

    private void Process(Session session, Func<Task> work)
    {
        lock (session.Gate) { session.Tail = Chain(session, session.Tail, work); }
    }

    private async Task Chain(Session session, Task previous, Func<Task> work)
    {
        await previous;
        await Task.Yield();
        if (session.State is not SessionState.Handshaking)
        {
            return;
        }

        try { await work(); }
        catch (Exception ex) { Fail(session, $"could not complete its handshake: {ex.Message}"); }
    }

    private async Task<bool> Send(Session session, object item)
    {
        using IMemoryOwner<byte> body = handshake!.Serialize(item);
        return await inner.Request(session.Connection, body.Memory, new PeerSendOptions { Priority = engineController.HighestPriority }, session.Aborted.Token);
    }

    private void OnReceived(PeerReceivedEventArgs args)
    {
        Session session = GetSession(args.Connection);
        Start(session);

        bool isHandshakePayload;
        lock (session.Gate) { isHandshakePayload = handshake is not null && session.State is SessionState.Handshaking; }

        if (isHandshakePayload)
        {
            byte[] body = args.Payload.ToArray();
            Process(session, () => OnHandshakePayload(session, body));
            return;
        }

        OnData(session, args);
    }

    private void OnData(Session session, PeerReceivedEventArgs args)
    {
        lock (session.Gate)
        {
            if (session.State is SessionState.Closed)
            {
                return;
            }
            if (session.State is not SessionState.Open)
            {
                if (session.Pending.Count < MaxBufferedPayloads)
                {
                    session.Pending.Add(args);
                }
                return;
            }
        }

        received.Publish(args);
    }

    private async Task OnHandshakePayload(Session session, byte[] body)
    {
        object item = handshake!.Deserialize(body);
        if (item.GetType() != handshake.Processor.ItemType)
        {
            throw new InvalidDataException($"expected a {handshake.Processor.ItemType.Name}");
        }

        await handshake.Processor.OnReceived(session.Initial, item);
    }

    private void Establish(Session session)
    {
        PeerConnection connection = session.Connection;
        lock (session.Gate)
        {
            if (session.State is not SessionState.Handshaking)
            {
                return;
            }
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
                if (session.State is SessionState.Closed)
                {
                    return;
                }
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
            if (session.State is not SessionState.Closed)
            {
                session.State = SessionState.Open;
            }
        }

        foreach (PeerReceivedEventArgs buffered in pending)
        {
            received.Publish(buffered);
        }
        session.Established.TrySetResult(true);
        session.Deadline.Cancel();
    }

    private bool WrongSerialAddress(PeerConnection connection, string userName)
    {
        if (connection.Info is not SerialConnectionInfo serial || connection.Point is not { OtherRemotes.Count: > 0 } point)
        {
            return false;
        }

        byte? address = string.Equals(point.User, userName, StringComparison.OrdinalIgnoreCase)
            ? point.RemoteSerialAddress
            : point.OtherRemotes.FirstOrDefault(remote => string.Equals(remote.User, userName, StringComparison.OrdinalIgnoreCase))?.Address;
        if (address is null || address == serial.RemoteSerialAddress)
        {
            return false;
        }

        serial.PreferredRemoteSerialAddress = address;
        return true;
    }

    private void Fail(Session session, string reason)
    {
        lock (session.Gate)
        {
            if (session.State is SessionState.Closed)
            {
                return;
            }
            session.State = SessionState.Closed;
            session.Pending.Clear();
        }

        logger.Record(LogEvents.ConnectionDropped, "Dropped a connection that {Reason}", reason);
        session.Established.TrySetResult(false);
        session.Deadline.Cancel();
        session.Aborted.Cancel();
        session.Connection.Drop();
    }

    private void OnDisconnected(PeerConnectionEventArgs args)
    {
        if (!sessions.TryGetValue(args.Connection, out Session? session))
        {
            return;
        }

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
        if (wasPublished)
        {
            disconnected.Publish(args);
        }
    }

    private UserIdentity? Identify(PeerConnection connection)
    {
        ConnectionInfo info = connection.Info;
        string? name = connection.InitialUser ?? info switch { SerialConnectionInfo serial => SerialUser(serial), IpConnectionInfo ip => MatchCertificate(ip.CertificateNames), _ => null };
        return name is null ? null : new UserIdentity { Name = name, Data = engineController.GetUserData(name) };
    }

    private string? SerialUser(SerialConnectionInfo info)
        => engineController.OutgoingPoints.Where(point => point.IsSerial && string.Equals(point.SerialPort, info.SerialPort, StringComparison.OrdinalIgnoreCase) && point.SerialAddress == info.SerialAddress)
            .Select(point => point.UserAt(info.RemoteSerialAddress)).FirstOrDefault(user => user is not null)
            ?? info.SerialPort;

    private string? MatchCertificate(IReadOnlyList<string> certificateNames)
    {
        foreach (string user in KnownUsers())
        {
            if (certificateNames.Contains(user, StringComparer.OrdinalIgnoreCase))
            {
                return user;
            }
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
        public IHandshakeSession Initial { get; set; } = null!;
    }

    private sealed class InitialSession(HandshakePeerTransport owner, Session session) : IHandshakeSession
    {
        public IConnectionInfo Connection => owner.engineController.WithLocalUser(session.Connection.Info);

        public IEngineContext Engine => owner.contexts?.Create() ?? new EngineContext(new UserInfo { Name = Connection.LocalUser ?? string.Empty }, owner.engineController.Users, owner.engineController.GetUserInfo, _ => false, owner.engineController.GetGroupMembers);

        public Task Connected(string userName)
        {
            if (owner.WrongSerialAddress(session.Connection, userName))
            {
                owner.Fail(session, "was reached at the wrong HDLC address for its user, so the link is formed again at the right one");
                return Task.CompletedTask;
            }

            session.Connection.InitialUser = userName;
            owner.Establish(session);
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            owner.Fail(session, "was disconnected by its initial processor");
            return Task.CompletedTask;
        }

        public async Task<bool> Send(object item)
        {
            if (session.State is SessionState.Closed)
            {
                return false;
            }

            try
            {
                if (await owner.Send(session, item))
                {
                    return true;
                }

                owner.Fail(session, "could not complete its handshake: an initial item was not accepted for sending");
            }
            catch (Exception ex)
            {
                owner.Fail(session, $"could not complete its handshake: {ex.Message}");
            }

            return false;
        }
    }
}
