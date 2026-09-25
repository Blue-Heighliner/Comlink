namespace BlueHeighliner.Comlink.Tests.Unit.Peer.Transport;

/// <summary>Unit tests for <see cref="SerialPeerTransport"/> and its <see cref="SerialLink"/>, over an in-memory cable.</summary>
public sealed class SerialPeerTransportTests
{
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");
    private static readonly UserEndpoint endpoint = new() { SerialPort = "SL0" };
    private static readonly TimeSpan timeout = TimeSpan.FromSeconds(5);

    private sealed record Pair(SerialPeerTransport A, SerialPeerTransport B, FakeMicroGateCable Cable, PeerCollector AConnections, PeerCollector BReceived) : IAsyncDisposable
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

    private static async Task<Pair> ConnectedPair(int maxPayloadSize = 4090, TimeSpan? requestTimeout = null)
    {
        FakeMicroGateCable cable = new(maxPayloadSize);
        SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20), requestTimeout);
        SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20), requestTimeout);
        PeerCollector aConnections = new();
        PeerCollector bReceived = new();
        a.Connected.Listen(args =>
        {
            aConnections.Connection = args.Connection;
            aConnections.AddEvent("connected");
        });
        a.Disconnected.Listen(_ => aConnections.AddEvent("disconnected"));
        b.Received.Listen(args => bReceived.AddPayload(args.Payload.ToArray()));

        a.Open(endpoint);
        b.Open(endpoint);
        await WaitUntil(() => aConnections.Events.Contains("connected"));
        return new Pair(a, b, cable, aConnections, bReceived);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(5);
        }
    }

    /// <summary>A request is delivered to the other end's Received and returns true once the other end has acknowledged it.</summary>
    [Fact]
    public async Task Request_DeliversPayloadAndReturnsTrueOnAcknowledgement()
    {
        await using Pair pair = await ConnectedPair();

        bool accepted = await pair.A.Request(endpoint, new byte[] { 1, 2, 3 }).WaitAsync(timeout);

        Assert.True(accepted);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.Single(pair.BReceived.Payloads));
    }

    /// <summary>A message larger than one HDLC frame is split into fragments and delivered to the other end whole, in order.</summary>
    [Fact]
    public async Task Request_LargerThanOneFrame_IsFragmentedAndReassembled()
    {
        await using Pair pair = await ConnectedPair(maxPayloadSize: 40);
        byte[] payload = Enumerable.Range(0, 1000).Select(i => (byte)(i % 251)).ToArray();

        bool accepted = await pair.A.Request(endpoint, payload).WaitAsync(timeout);

        Assert.True(accepted);
        Assert.Equal(payload, Assert.Single(pair.BReceived.Payloads));
    }

    /// <summary>Several concurrent requests each get their own acknowledgement and none of their fragments are mixed up.</summary>
    [Fact]
    public async Task Request_Concurrent_AllDeliveredIntact()
    {
        await using Pair pair = await ConnectedPair(maxPayloadSize: 40);
        byte[][] payloads = [.. Enumerable.Range(1, 8).Select(n => Enumerable.Repeat((byte)n, 200).ToArray())];

        bool[] results = await Task.WhenAll(payloads.Select(p => pair.A.Request(endpoint, p))).WaitAsync(timeout);

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

        bool accepted = await pair.A.Request(endpoint, ReadOnlyMemory<byte>.Empty).WaitAsync(timeout);

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

        Assert.True(await pair.B.Request(endpoint, new byte[] { 9 }).WaitAsync(timeout));

        Assert.Equal(new byte[] { 9 }, Assert.Single(aReceived.Payloads));
    }

    /// <summary>The Transmitted callback fires once the payload has been handed to the link, before the send completes.</summary>
    [Fact]
    public async Task Request_InvokesTransmittedCallback()
    {
        await using Pair pair = await ConnectedPair();
        int transmitted = 0;

        await pair.A.Request(endpoint, new byte[] { 1 }, new PeerSendOptions { Transmitted = () => transmitted++ }).WaitAsync(timeout);

        Assert.Equal(1, transmitted);
    }

    /// <summary>A received message's connection is the link's own connection, identified by its configured endpoint and never inbound.</summary>
    [Fact]
    public async Task Received_ConnectionCarriesConfiguredEndpoint()
    {
        await using Pair pair = await ConnectedPair();
        PeerConnection? connection = null;
        pair.B.Received.Listen(args => connection = args.Connection);

        await pair.A.Request(endpoint, new byte[] { 1 }).WaitAsync(timeout);

        Assert.NotNull(connection);
        Assert.Equal(endpoint, connection.Endpoint);
        Assert.False(connection.IsInbound);
        Assert.Null(connection.IdentitySubject);
    }

    /// <summary>Requesting before the link has come up fails immediately instead of waiting.</summary>
    [Fact]
    public async Task Request_NotConnected_ThrowsIOException()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<IOException>(() => transport.Request(endpoint, new byte[] { 1 }));
    }

    /// <summary>A request whose other end never answers fails with IOException once the request timeout elapses.</summary>
    [Fact]
    public async Task Request_NoReply_TimesOut()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(100));
        IMicroGatePeer silent = cable.EndB.Create();
        _ = silent.Start("SL0").AsTask();
        transport.Open(endpoint);
        TaskCompletionSource up = new();
        transport.Connected.Listen(_ => up.TrySetResult());
        await up.Task.WaitAsync(timeout);

        await Assert.ThrowsAsync<IOException>(() => transport.Request(endpoint, new byte[] { 1 }));
    }

    /// <summary>Connected and Disconnected are published as the link comes up and is lost, and the link comes back on its own.</summary>
    [Fact]
    public async Task Link_CableCut_DisconnectsThenReconnects()
    {
        await using Pair pair = await ConnectedPair();

        pair.Cable.Cut();

        await WaitUntil(() => pair.AConnections.Events.SequenceEqual(["connected", "disconnected", "connected"]));
        Assert.True(await pair.A.Request(endpoint, new byte[] { 5 }).WaitAsync(timeout));
    }

    /// <summary>A request in flight when the link is lost fails with IOException rather than hanging.</summary>
    [Fact]
    public async Task Request_LinkLostWhileWaiting_ThrowsIOException()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20), TimeSpan.FromSeconds(30));
        IMicroGatePeer silent = cable.EndB.Create();
        _ = silent.Start("SL0").AsTask();
        transport.Open(endpoint);
        TaskCompletionSource up = new();
        transport.Connected.Listen(_ => up.TrySetResult());
        await up.Task.WaitAsync(timeout);

        Task<bool> request = transport.Request(endpoint, new byte[] { 1 });
        cable.Cut();

        await Assert.ThrowsAsync<IOException>(() => request.WaitAsync(timeout));
    }

    /// <summary>A device that cannot be opened is retried until it can, without ever surfacing an exception to the caller.</summary>
    [Fact]
    public async Task Link_StartFailure_IsRetriedUntilItSucceeds()
    {
        FakeMicroGateCable cable = new() { StartFailure = new IOException("no such port") };
        await using SerialPeerTransport a = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        await using SerialPeerTransport b = new(cable.EndB, logger, TimeSpan.FromMilliseconds(20));
        TaskCompletionSource up = new();
        a.Connected.Listen(_ => up.TrySetResult());
        a.Open(endpoint);
        b.Open(endpoint);

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
        Assert.True(await pair.A.Request(endpoint, new byte[] { 5 }).WaitAsync(timeout));
    }

    /// <summary>Two endpoints naming the same port and address share one link; a different address is a separate link.</summary>
    [Fact]
    public async Task Open_SameEndpointTwice_SharesOneLink()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));

        transport.Open(new UserEndpoint { SerialPort = "SL0", SerialAddress = 1 });
        transport.Open(new UserEndpoint { SerialPort = "sl0", SerialAddress = 1 });
        transport.Open(new UserEndpoint { SerialPort = "SL0", SerialAddress = 2 });
        await WaitUntil(() => cable.PeersCreated >= 2);
        await Task.Delay(50);

        Assert.Equal(2, cable.PeersCreated);
    }

    /// <summary>An IP endpoint is rejected: this transport only carries serial.</summary>
    [Fact]
    public async Task Request_IpEndpoint_ThrowsArgumentException()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger);

        await Assert.ThrowsAsync<ArgumentException>(() => transport.Request(new UserEndpoint { IpAddress = "10.0.0.1", Port = 1 }, new byte[] { 1 }));
    }

    /// <summary>A message too large to number its fragments is refused up front rather than sent partially.</summary>
    [Fact]
    public async Task Request_TooManyFragments_ThrowsArgumentOutOfRange()
    {
        await using Pair pair = await ConnectedPair(maxPayloadSize: SerialFrame.DataHeaderSize + 1);
        byte[] huge = new byte[ushort.MaxValue + 10];

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => pair.A.Request(endpoint, huge));
    }

    /// <summary>Disposing the transport stops the reconnect loop and disposes the device peer.</summary>
    [Fact]
    public async Task DisposeAsync_StopsLinkAndDisposesPeer()
    {
        FakeMicroGateCable cable = new();
        SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        transport.Open(endpoint);
        await WaitUntil(() => cable.PeersCreated >= 1);

        await transport.DisposeAsync();
        int created = cable.PeersCreated;
        await Task.Delay(100);

        Assert.Equal(created, cable.PeersCreated);
    }

    /// <summary>Frames that are not part of the protocol are ignored without disturbing the link.</summary>
    [Fact]
    public async Task Received_GarbageFrame_IsIgnored()
    {
        FakeMicroGateCable cable = new();
        await using SerialPeerTransport transport = new(cable.EndA, logger, TimeSpan.FromMilliseconds(20));
        IMicroGatePeer raw = cable.EndB.Create();
        _ = raw.Start("SL0").AsTask();
        transport.Open(endpoint);
        PeerCollector received = new();
        transport.Received.Listen(args => received.AddPayload(args.Payload.ToArray()));
        await WaitUntil(() => raw.IsConnected);

        await raw.Send(new byte[] { 0xEE, 1, 2, 3 });
        await raw.Send(SerialFrame.EncodeData(5, 1, 3, [1]));
        await raw.Send(SerialFrame.EncodeData(6, 0, 1, [7, 7]));

        await WaitUntil(() => received.Payloads.Count == 1);
        Assert.Equal(new byte[] { 7, 7 }, received.Payloads[0]);
    }
}
