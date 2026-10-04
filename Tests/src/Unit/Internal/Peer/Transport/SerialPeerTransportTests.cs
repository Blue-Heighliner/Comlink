namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="SerialPeerTransport"/> and its <see cref="SerialLink"/>, over an in-memory cable.</summary>
public sealed class SerialPeerTransportTests
{
    private static async Task StartAndConnect(IHdlcPeer peer)
    {
        await peer.Start("SL0");
        await peer.Connect(255, 255);
    }

    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");
    private static readonly ConnectionPoint point = new() { SerialPort = "SL0" };
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(30);

    private sealed record Pair(SerialPeerTransport A, SerialPeerTransport B, FakeHdlcCable Cable, PeerCollector AConnections, PeerCollector BReceived) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await A.DisposeAsync();
            await B.DisposeAsync();
        }
    }

    private sealed class PeerCollector
    {
        private readonly Lock gate = new();
        private readonly List<byte[]> payloads = [];
        private readonly List<string> events = [];

        public IReadOnlyList<byte[]> Payloads { get { lock (gate) { return [.. payloads]; } } }

        public IReadOnlyList<string> Events { get { lock (gate) { return [.. events]; } } }

        public PeerConnection? Connection { get; set; }

        public void AddPayload(byte[] payload) { lock (gate) { payloads.Add(payload); } }

        public void AddEvent(string name) { lock (gate) { events.Add(name); } }
    }

    private static async Task<Pair> ConnectedPair(int maxPayloadSize = 4090)
    {
        FakeHdlcCable cable = new(maxPayloadSize);
        SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20));
        PeerCollector aConnections = new();
        PeerCollector bReceived = new();
        a.Connected.Listen(args =>
        {
            aConnections.Connection = args.Connection;
            aConnections.AddEvent("connected");
        });
        a.Disconnected.Listen(_ => aConnections.AddEvent("disconnected"));
        b.Received.Listen(args => bReceived.AddPayload(args.Payload.ToArray()));

        Open(a, point);
        Open(b, point);
        await WaitUntil(() => aConnections.Events.Contains("connected"));
        return new Pair(a, b, cable, aConnections, bReceived);
    }

    private static void Open(SerialPeerTransport transport, ConnectionPoint point)
        => transport.Connect(point).ContinueWith(static task => _ = task.Exception, TaskScheduler.Default);

    private static async Task<bool> Request(SerialPeerTransport transport, ConnectionPoint point, ReadOnlyMemory<byte> data, PeerSendOptions? options = null)
        => await transport.Request(await transport.Connect(point), data, options);

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(5);
        }
    }

    /// <summary>A request is delivered to the other end's Received and returns true once it is on the link.</summary>
    [Fact]
    public async Task Request_DeliversPayloadAndReturnsTrue()
    {
        await using Pair pair = await ConnectedPair();

        bool accepted = await Request(pair.A, point, new byte[] { 1, 2, 3 }).WaitAsync(timeout);

        Assert.True(accepted);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(pair.BReceived.Payloads));
    }

    /// <summary>A payload larger than one HDLC frame fails to send up front, with the reason logged, and is never split or sent partially.</summary>
    [Fact]
    public async Task Request_LargerThanOneFrame_FailsWithoutSending()
    {
        await using Pair pair = await ConnectedPair(maxPayloadSize: 40);

        Assert.False(await Request(pair.A, point, new byte[41]));
        Assert.Empty(pair.BReceived.Payloads);
    }

    /// <summary>Each payload is exactly one HDLC frame carrying the payload bytes and nothing else.</summary>
    [Fact]
    public async Task Request_SendsOnlyThePayloadInOneFrame()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        IHdlcPeer raw = cable.EndB.Create();
        List<byte[]> frames = [];
        raw.Receiver = owner => { using (owner) { lock (frames) { frames.Add(owner.Memory.ToArray()); } } };
        _ = StartAndConnect(raw);
        Open(transport, point);
        TaskCompletionSource up = new();
        transport.Connected.Listen(_ => up.TrySetResult());
        await up.Task.WaitAsync(timeout);

        await Request(transport, point, new byte[] { 9, 8, 7 }).WaitAsync(timeout);
        await Request(transport, point, ReadOnlyMemory<byte>.Empty).WaitAsync(timeout);

        await WaitUntil(() => { lock (frames) { return frames.Count == 2; } });
        Assert.Equal(new byte[] { 9, 8, 7 }, frames[0]);
        Assert.Empty(frames[1]);
    }

    /// <summary>Several concurrent requests are each delivered intact.</summary>
    [Fact]
    public async Task Request_Concurrent_AllDeliveredIntact()
    {
        await using Pair pair = await ConnectedPair(maxPayloadSize: 40);
        byte[][] payloads = [.. Enumerable.Range(1, 8).Select(n => Enumerable.Repeat((byte)n, 30).ToArray())];

        bool[] results = await Task.WhenAll(payloads.Select(p => Request(pair.A, point, p))).WaitAsync(timeout);

        Assert.All(results, Assert.True);
        await WaitUntil(() => pair.BReceived.Payloads.Count == payloads.Length);
        foreach (byte[] payload in payloads)
        {
            Assert.Contains(pair.BReceived.Payloads, received => received.SequenceEqual(payload));
        }
    }

    /// <summary>An empty payload (a heartbeat) is delivered as an empty message and acknowledged.</summary>
    [Fact]
    public async Task Request_EmptyPayload_DeliveredAsEmptyMessage()
    {
        await using Pair pair = await ConnectedPair();

        bool accepted = await Request(pair.A, point, ReadOnlyMemory<byte>.Empty).WaitAsync(timeout);

        Assert.True(accepted);
        Assert.Empty(Assert.Single(pair.BReceived.Payloads));
    }

    /// <summary>Messages flow both ways over the same single link.</summary>
    [Fact]
    public async Task Request_BothDirections_Work()
    {
        await using Pair pair = await ConnectedPair();
        PeerCollector aReceived = new();
        pair.A.Received.Listen(args => aReceived.AddPayload(args.Payload.ToArray()));

        Assert.True(await Request(pair.B, point, new byte[] { 9 }).WaitAsync(timeout));

        Assert.Equal(new byte[] { 9 }, Assert.Single(aReceived.Payloads));
    }

    /// <summary>The Transmitted callback fires once the payload has been handed to the link, before the send completes.</summary>
    [Fact]
    public async Task Request_InvokesTransmittedCallback()
    {
        await using Pair pair = await ConnectedPair();
        int transmitted = 0;

        await Request(pair.A, point, new byte[] { 1 }, new PeerSendOptions { Transmitted = () => transmitted++ }).WaitAsync(timeout);

        Assert.Equal(1, transmitted);
    }

    /// <summary>A received message's connection is the link's own connection, describing the port and address it runs over and never inbound.</summary>
    [Fact]
    public async Task Received_ConnectionCarriesConfiguredPoint()
    {
        await using Pair pair = await ConnectedPair();
        PeerConnection? connection = null;
        pair.B.Received.Listen(args => connection = args.Connection);

        await Request(pair.A, point, new byte[] { 1 }).WaitAsync(timeout);

        Assert.NotNull(connection);
        Assert.Equal(point, connection.Point);
        Assert.False(connection.IsInbound);
        ISerialConnectionInfo serial = Assert.IsAssignableFrom<ISerialConnectionInfo>(connection.Info);
        Assert.Equal("SL0", serial.SerialPort);
        Assert.Equal(0xFF, serial.SerialAddress);
    }

    /// <summary>Every serial link starts its peer with the configured options and the point's own and remote station addresses.</summary>
    [Fact]
    public async Task Connect_StartsPeersWithTheConfiguredOptionsAndPointAddress()
    {
        FakeHdlcCable cable = new();
        HdlcPeerOptions options = new() { MaxInfoField = 512 };
        await using SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20), options: options);
        await using SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20), options: options);
        ConnectionPoint addressed = new() { SerialPort = "SL0", SerialAddress = 7, RemoteSerialAddress = 9 };
        Open(a, addressed);
        Open(b, addressed);

        await WaitUntil(() => cable.Starts.Count == 2);

        Assert.All(cable.Starts, start =>
        {
            Assert.Equal((7, 9), (start.Address, start.RemoteAddress));
            Assert.Same(options, start.Options);
        });
    }

    /// <summary>With several users that may be at the far end of the cable, a link tries each one's address in turn until the end that answers is found, and the connection is identified as the user at that address.</summary>
    [Fact]
    public async Task Connect_SeveralRemotes_TriesEachUntilTheFarEndAnswers()
    {
        FakeHdlcCable cable = new() { EnforcesAddresses = true };
        await using SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20), candidateTimeout: TimeSpan.FromMilliseconds(150));
        await using SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20));
        ConnectionPoint wrongFirst = new() { SerialPort = "SL0", SerialAddress = 1, RemoteSerialAddress = 2, User = "NOBODY", OtherRemotes = [new HdlcRemote("BOB", 3)] };
        ConnectionPoint far = new() { SerialPort = "SL0", SerialAddress = 3, RemoteSerialAddress = 1, User = "ALICE" };
        Open(b, far);
        Open(a, wrongFirst);

        await WaitUntil(() => a.Connect(wrongFirst).IsCompletedSuccessfully);

        PeerConnection connection = await a.Connect(wrongFirst);
        Assert.Equal(3, ((ISerialConnectionInfo)connection.Info).RemoteSerialAddress);
        Assert.Equal("BOB", wrongFirst.UserAt(((ISerialConnectionInfo)connection.Info).RemoteSerialAddress));
        Assert.Contains(cable.Starts, start => start.Address == 1 && start.RemoteAddress == 2);
        Assert.Contains(cable.Starts, start => start.Address == 1 && start.RemoteAddress == 3);
    }

    /// <summary>Requesting before the link has come up fails immediately instead of waiting.</summary>
    [Fact]
    public async Task Request_NotConnected_ThrowsIOException()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<IOException>(() => Request(transport, point, new byte[] { 1 }));
    }

    /// <summary>Connected and Disconnected are published as the link comes up and is lost, and the link comes back on its own.</summary>
    [Fact]
    public async Task Link_CableCut_DisconnectsThenReconnects()
    {
        await using Pair pair = await ConnectedPair();

        pair.Cable.Cut();

        await WaitUntil(() => pair.AConnections.Events.SequenceEqual(["connected", "disconnected", "connected"]));
        Assert.True(await Request(pair.A, point, new byte[] { 5 }).WaitAsync(timeout));
    }

    /// <summary>A device that cannot be opened is retried until it can, without ever surfacing an exception to the caller.</summary>
    [Fact]
    public async Task Link_StartFailure_IsRetriedUntilItSucceeds()
    {
        FakeHdlcCable cable = new() { StartFailure = new IOException("no such port") };
        await using SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        await using SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20));
        TaskCompletionSource up = new();
        a.Connected.Listen(_ => up.TrySetResult());
        Open(a, point);
        Open(b, point);

        await WaitUntil(() => cable.PeersCreated >= 4);
        cable.StartFailure = null;

        await up.Task.WaitAsync(timeout);
    }

    /// <summary>Dropping the connection closes the link, which is then re-established.</summary>
    [Fact]
    public async Task Connection_Drop_ClosesLinkAndItReconnects()
    {
        await using Pair pair = await ConnectedPair();

        pair.AConnections.Connection!.Drop();

        await WaitUntil(() => pair.AConnections.Events.SequenceEqual(["connected", "disconnected", "connected"]));
        Assert.True(await Request(pair.A, point, new byte[] { 5 }).WaitAsync(timeout));
    }

    /// <summary>Closing a link disconnects it and stops it reconnecting, and requests to it fail immediately as closed.</summary>
    [Fact]
    public async Task SetClosed_DisconnectsAndStaysDown()
    {
        await using Pair pair = await ConnectedPair();

        pair.A.SetClosed(point, true);

        await WaitUntil(() => pair.AConnections.Events.SequenceEqual(["connected", "disconnected"]));
        await Task.Delay(150);
        Assert.Equal(["connected", "disconnected"], pair.AConnections.Events);
        IOException error = await Assert.ThrowsAsync<IOException>(() => Request(pair.A, point, new byte[] { 1 }));
        Assert.Contains("closed", error.Message);
    }

    /// <summary>Reopening a closed link brings it back up.</summary>
    [Fact]
    public async Task SetClosed_ThenReopened_Reconnects()
    {
        await using Pair pair = await ConnectedPair();
        pair.A.SetClosed(point, true);
        await WaitUntil(() => pair.AConnections.Events.Contains("disconnected"));

        pair.A.SetClosed(point, false);

        await WaitUntil(() => pair.AConnections.Events.Count(e => e == "connected") == 2);
        Assert.True(await Request(pair.A, point, new byte[] { 3 }).WaitAsync(timeout));
    }

    /// <summary>A link closed before it was ever opened never touches the device until it is reopened.</summary>
    [Fact]
    public async Task SetClosed_BeforeAnythingOpened_CreatesNoPeerUntilReopened()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        transport.SetClosed(point, true);
        await Task.Delay(100);
        Assert.Equal(0, cable.PeersCreated);

        transport.SetClosed(point, false);
        await WaitUntil(() => cable.PeersCreated >= 1);
    }

    /// <summary>Closing a link that is still waiting for the other end abandons the attempt instead of hanging on to the device.</summary>
    [Fact]
    public async Task SetClosed_WhileConnecting_AbandonsAttempt()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        Open(transport, point);
        await WaitUntil(() => cable.PeersCreated == 1);

        transport.SetClosed(point, true);
        await Task.Delay(150);

        Assert.Equal(1, cable.PeersCreated);
    }

    /// <summary>Reset drops the link and it comes back up on its own; on a closed link it does nothing.</summary>
    [Fact]
    public async Task Reset_ReconnectsOpenLink_IgnoredWhenClosed()
    {
        await using Pair pair = await ConnectedPair();

        pair.A.Reset(point);
        await WaitUntil(() => pair.AConnections.Events.SequenceEqual(["connected", "disconnected", "connected"]));

        pair.A.SetClosed(point, true);
        await WaitUntil(() => pair.AConnections.Events.Count(e => e == "disconnected") == 2);
        pair.A.Reset(point);
        await Task.Delay(100);
        Assert.Equal(4, pair.AConnections.Events.Count);
    }

    /// <summary>Reset on an point that was never opened does not open it.</summary>
    [Fact]
    public async Task Reset_UnopenedPoint_DoesNotOpenIt()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        transport.Reset(point);
        await Task.Delay(50);

        Assert.Equal(0, cable.PeersCreated);
    }

    /// <summary>Two points naming the same port and address share one link; a different address is a separate link.</summary>
    [Fact]
    public async Task Open_SamePointTwice_SharesOneLink()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        Open(transport, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 1 });
        Open(transport, new ConnectionPoint { SerialPort = "sl0", SerialAddress = 1 });
        Open(transport, new ConnectionPoint { SerialPort = "SL0", SerialAddress = 2 });
        await WaitUntil(() => cable.PeersCreated >= 2);
        await Task.Delay(50);

        Assert.Equal(2, cable.PeersCreated);
    }

    /// <summary>An IP point is rejected: this transport only carries serial.</summary>
    [Fact]
    public async Task Request_IpPoint_ThrowsArgumentException()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger);

        await Assert.ThrowsAsync<ArgumentException>(() => Request(transport, new ConnectionPoint { IpAddress = "10.0.0.1", Port = 1 }, new byte[] { 1 }));
    }

    /// <summary>Disposing the transport stops the reconnect loop and disposes the device peer.</summary>
    [Fact]
    public async Task DisposeAsync_StopsLinkAndDisposesPeer()
    {
        FakeHdlcCable cable = new();
        SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        Open(transport, point);
        await WaitUntil(() => cable.PeersCreated >= 1);

        await transport.DisposeAsync();
        int created = cable.PeersCreated;
        await Task.Delay(100);

        Assert.Equal(created, cable.PeersCreated);
    }

    /// <summary>Whatever arrives in an information frame is delivered verbatim as one payload, since the link adds no framing of its own.</summary>
    [Fact]
    public async Task Received_EveryFrame_IsDeliveredVerbatim()
    {
        FakeHdlcCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        IHdlcPeer raw = cable.EndB.Create();
        _ = StartAndConnect(raw);
        Open(transport, point);
        PeerCollector received = new();
        transport.Received.Listen(args => received.AddPayload(args.Payload.ToArray()));
        await WaitUntil(() => raw.IsConnected);

        await raw.Send(new byte[] { 0xEE, 1, 2, 3 });
        await raw.Send(new byte[] { 7, 7 });

        await WaitUntil(() => received.Payloads.Count == 2);
        Assert.Equal(new byte[] { 0xEE, 1, 2, 3 }, received.Payloads[0]);
        Assert.Equal(new byte[] { 7, 7 }, received.Payloads[1]);
    }

    /// <summary>Closing an already closed link, and disposing twice, are both harmless.</summary>
    [Fact]
    public async Task SetClosedTwice_ThenDisposeTwice_DoesNotThrow()
    {
        await using Pair pair = await ConnectedPair();

        pair.A.SetClosed(point, true);
        pair.A.SetClosed(point, true);
        await WaitUntil(() => pair.AConnections.Events.Contains("disconnected"));
        await pair.A.DisposeAsync();
        await pair.A.DisposeAsync();
    }
}
