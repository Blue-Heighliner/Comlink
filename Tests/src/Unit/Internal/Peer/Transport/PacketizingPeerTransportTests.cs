namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer.Transport;

/// <summary>Unit tests for <see cref="PacketizingPeerTransport"/>.</summary>
public sealed class PacketizingPeerTransportTests
{
    private static readonly PeerConnection target = new(new ConnectionPoint { IpAddress = "10.0.0.5", Port = 4000 }, new IpConnectionInfo { Host = "10.0.0.5", Port = 4000 }, () => { });
    private static readonly ILogger logger = LoggerFactory.Create(_ => { }).CreateLogger("test");
    private static int Header => RawPacketSerializer.HeaderSize;

    private sealed record Sent(byte[] Data, PeerSendOptions? Options);

    private sealed record Fixture(
        PacketizingPeerTransport Transport,
        Mock<IPeerTransport> Inner,
        TestObservable<PeerReceivedEventArgs> Received,
        TestObservable<PeerConnectionEventArgs> Connected,
        TestObservable<PeerConnectionEventArgs> Disconnected,
        List<Sent> Sends);

    private sealed class TrackedOwner(byte[] data) : IMemoryOwner<byte>
    {
        public bool IsDisposed { get; private set; }
        public Memory<byte> Memory { get; } = data;
        public void Dispose() => IsDisposed = true;
    }

    private sealed class TrackingPacketizer(List<TrackedOwner> owners) : IPacketizer
    {
        public IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority, object? frame = null)
        {
            List<Packet> packets = [];
            for (byte i = 0; i < 3; i++)
            {
                TrackedOwner owner = new([i]);
                owners.Add(owner);
                packets.Add(new Packet { Data = owner, Priority = priority });
            }

            return packets;
        }

        public IPacketAssembler CreateAssembler() => throw new NotSupportedException();
    }

    private static Fixture Build(IPacketizer? packetizer = null, Func<Sent, Task<bool>>? respond = null, int? packetSize = null, int window = 1)
    {
        Mock<IPeerTransport> inner = new();
        TestObservable<PeerReceivedEventArgs> received = new();
        TestObservable<PeerConnectionEventArgs> connected = new();
        TestObservable<PeerConnectionEventArgs> disconnected = new();
        List<Sent> sends = [];
        inner.SetupGet(t => t.Received).Returns(received);
        inner.SetupGet(t => t.Connected).Returns(connected);
        inner.SetupGet(t => t.Disconnected).Returns(disconnected);
        inner.Setup(t => t.Request(It.IsAny<PeerConnection>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<PeerSendOptions>(), It.IsAny<CancellationToken>()))
            .Returns<PeerConnection, ReadOnlyMemory<byte>, PeerSendOptions?, CancellationToken>((_, data, options, _) =>
            {
                Sent sent = new(data.ToArray(), options);
                lock (sends) { sends.Add(sent); }
                return respond?.Invoke(sent) ?? Task.FromResult(true);
            });
        return new Fixture(new PacketizingPeerTransport(inner.Object, packetizer ?? new Packetizer(new RawPacketEngineController(packetSize ?? Header + 10)), window, logger), inner, received, connected, disconnected, sends);
    }

    private sealed class Manual
    {
        private readonly List<TaskCompletionSource<bool>> pending = [];

        public Task<bool> Respond(Sent sent)
        {
            TaskCompletionSource<bool> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (pending) { pending.Add(source); }
            return source.Task;
        }

        public void Complete(int index)
        {
            lock (pending) { pending[index].SetResult(true); }
        }
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) { throw new TimeoutException("Condition was not met in time."); }
            await Task.Delay(5);
        }
    }

    private static byte[] Payload(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

    private static PeerConnection Connection() => new(null, new IpConnectionInfo { IsInbound = true }, () => { });

    private static List<byte[]> Packets(byte[] payload)
    {
        Packetizer packetizer = new(new RawPacketEngineController(Header + 10));
        IReadOnlyList<Packet> packets = packetizer.Split(payload, 0);
        List<byte[]> bytes = [.. packets.Select(p => p.Data.Memory.ToArray())];
        foreach (Packet packet in packets) { packet.Dispose(); }
        return bytes;
    }

    /// <summary>A payload that is already a packet is sent as it is, not split.</summary>
    [Fact]
    public async Task Request_AlreadyAPacket_IsSentUntouched()
    {
        Fixture fx = Build();
        byte[] packet = Payload(35);

        bool ok = await fx.Transport.Request(target, packet, new PeerSendOptions { Priority = 3, IsPacket = true });

        Assert.True(ok);
        Sent sent = Assert.Single(fx.Sends);
        Assert.Equal(packet, sent.Data);
        Assert.Equal(3, sent.Options!.Priority);
    }

    /// <summary>A payload is sent as several packets at the payload's priority, which reassemble into the original payload.</summary>
    [Fact]
    public async Task Request_SplitsPayloadIntoPacketsAtItsPriority()
    {
        Fixture fx = Build();
        byte[] payload = Payload(35);

        bool ok = await fx.Transport.Request(target, payload, new PeerSendOptions { Priority = 7 });

        Assert.True(ok);
        Assert.Equal(4, fx.Sends.Count);
        Assert.All(fx.Sends, sent => Assert.Equal(7, sent.Options!.Priority));
        using IPacketAssembler assembler = new Packetizer(new RawPacketEngineController(Header + 10)).CreateAssembler();
        AssembledPayload? complete = null;
        foreach (Sent sent in fx.Sends) { complete = assembler.Add(sent.Data) ?? complete; }
        using IMemoryOwner<byte> owner = Assert.IsType<AssembledPayload>(complete).Payload;
        Assert.Equal(payload, owner.Memory.ToArray());
    }

    private sealed class FrameSpyPacketizer(List<object?> frames) : IPacketizer
    {
        public IReadOnlyList<Packet> Split(ReadOnlyMemory<byte> payload, int priority, object? frame = null)
        {
            frames.Add(frame);
            return [new Packet { Data = new TrackedOwner([1]), Priority = priority }];
        }

        public IPacketAssembler CreateAssembler() => throw new NotSupportedException();
    }

    /// <summary>The frame a send states is handed to the packetizer so the packet serializer can see it.</summary>
    [Fact]
    public async Task Request_FrameOption_IsHandedToThePacketizer()
    {
        List<object?> frames = [];
        Fixture fx = Build(new FrameSpyPacketizer(frames));
        object frame = new();

        await fx.Transport.Request(target, new byte[] { 1 }, new PeerSendOptions { Frame = frame });
        await fx.Transport.Request(target, new byte[] { 1 });

        Assert.Same(frame, frames[0]);
        Assert.Null(frames[1]);
    }

    /// <summary>A reassembled payload is published with the first packet that carried it.</summary>
    [Fact]
    public async Task Received_ReassembledPayload_CarriesTheFirstPacket()
    {
        Fixture fx = Build();
        PeerConnection connection = Connection();
        List<PeerReceivedEventArgs> published = [];
        fx.Transport.Received.Listen(published.Add);
        byte[] payload = Payload(35);

        foreach (byte[] packet in Packets(payload)) { fx.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = packet }); }

        await WaitUntil(() => published.Count == 1);
        Assert.Equal(payload, published[0].Payload.ToArray());
        Assert.Equal(0, Assert.IsType<TestPacket>(published[0].Packet).Index);
    }

    /// <summary>The wrapped transport is handed one packet at a time, so nothing is queued there that a more urgent payload could not overtake.</summary>
    [Fact]
    public async Task Request_HandsWrappedTransportOnePacketAtATime()
    {
        int inFlight = 0;
        int maxInFlight = 0;
        Fixture fx = Build(respond: async _ =>
        {
            int now = Interlocked.Increment(ref inFlight);
            maxInFlight = Math.Max(maxInFlight, now);
            await Task.Delay(5);
            Interlocked.Decrement(ref inFlight);
            return true;
        });

        bool[] results = await Task.WhenAll(fx.Transport.Request(target, Payload(35)), fx.Transport.Request(target, Payload(35)), fx.Transport.Request(target, Payload(35)));

        Assert.All(results, Assert.True);
        Assert.Equal(12, fx.Sends.Count);
        Assert.Equal(1, maxInFlight);
    }

    /// <summary>The configured packet size caps every packet put on the wire, framing included.</summary>
    [Fact]
    public async Task Request_HonorsConfiguredPacketSize()
    {
        Fixture fx = Build(packetSize: Header + 20);

        await fx.Transport.Request(target, Payload(50));

        Assert.Equal(3, fx.Sends.Count);
        Assert.All(fx.Sends, sent => Assert.True(sent.Data.Length <= Header + 20));
    }

    /// <summary>A wider window keeps that many packets in flight at once, and never more.</summary>
    [Fact]
    public async Task Request_WiderWindow_KeepsThatManyPacketsInFlight()
    {
        int inFlight = 0;
        int maxInFlight = 0;
        Manual manual = new();
        Fixture fx = Build(respond: sent =>
        {
            int now = Interlocked.Increment(ref inFlight);
            maxInFlight = Math.Max(maxInFlight, now);
            return manual.Respond(sent).ContinueWith(task =>
            {
                Interlocked.Decrement(ref inFlight);
                return task.Result;
            });
        }, window: 3);

        Task<bool> request = fx.Transport.Request(target, Payload(100));
        await WaitUntil(() => fx.Sends.Count == 3);
        await Task.Delay(50);
        Assert.Equal(3, fx.Sends.Count);

        for (int i = 0; i < 10; i++)
        {
            manual.Complete(i);
            if (i < 7) { await WaitUntil(() => fx.Sends.Count == Math.Min(i + 4, 10)); }
        }

        Assert.True(await request);
        Assert.Equal(10, fx.Sends.Count);
        Assert.Equal(3, maxInFlight);
    }

    /// <summary>With a wider window a higher-priority payload can wait behind that many lower-priority packets, then goes ahead of the rest.</summary>
    [Fact]
    public async Task Request_WiderWindow_HigherPriorityWaitsBehindWindowThenOvertakes()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond, window: 2);

        Task<bool> low = fx.Transport.Request(target, Payload(35), new PeerSendOptions { Priority = 1 });
        await WaitUntil(() => fx.Sends.Count == 2);
        Task<bool> high = fx.Transport.Request(target, Payload(15), new PeerSendOptions { Priority = 9 });
        await Task.Delay(50);
        Assert.Equal(2, fx.Sends.Count);

        manual.Complete(0);
        await WaitUntil(() => fx.Sends.Count == 3);
        manual.Complete(1);
        await WaitUntil(() => fx.Sends.Count == 4);
        for (int i = 2; i < 6; i++)
        {
            manual.Complete(i);
            if (i < 4) { await WaitUntil(() => fx.Sends.Count == i + 3); }
        }

        Assert.True(await low);
        Assert.True(await high);
        Assert.Equal([1, 1, 9, 9, 1, 1], fx.Sends.Select(sent => sent.Options!.Priority));
    }

    /// <summary>A higher-priority payload sent while a lower-priority one is being transmitted goes out right after the packet already in flight, and the lower-priority packets wait until it is done.</summary>
    [Fact]
    public async Task Request_HigherPriorityPayload_OvertakesRemainingLowerPriorityPackets()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);

        Task<bool> low = fx.Transport.Request(target, Payload(35), new PeerSendOptions { Priority = 1 });
        await WaitUntil(() => fx.Sends.Count == 1);
        Task<bool> high = fx.Transport.Request(target, Payload(15), new PeerSendOptions { Priority = 9 });
        await Task.Delay(50);
        Assert.Single(fx.Sends);

        for (int i = 0; i < 6; i++)
        {
            manual.Complete(i);
            if (i < 5) { await WaitUntil(() => fx.Sends.Count == i + 2); }
        }

        Assert.True(await low);
        Assert.True(await high);
        Assert.Equal([1, 9, 9, 1, 1, 1], fx.Sends.Select(sent => sent.Options!.Priority));
    }

    /// <summary>Payloads of equal priority are sent one after the other, never interleaved.</summary>
    [Fact]
    public async Task Request_EqualPriorityPayloads_AreSentInOrder()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);
        byte[] first = Payload(25);
        byte[] second = [.. Payload(25).Select(b => (byte)(b + 100))];

        Task<bool> a = fx.Transport.Request(target, first, new PeerSendOptions { Priority = 4 });
        Task<bool> b = fx.Transport.Request(target, second, new PeerSendOptions { Priority = 4 });
        for (int i = 0; i < 6; i++)
        {
            await WaitUntil(() => fx.Sends.Count == i + 1);
            manual.Complete(i);
        }

        Assert.True(await a);
        Assert.True(await b);
        Assert.Equal(first[0], fx.Sends[0].Data[Header]);
        Assert.Equal(first[10], fx.Sends[1].Data[Header]);
        Assert.Equal(first[20], fx.Sends[2].Data[Header]);
        Assert.Equal(second[0], fx.Sends[3].Data[Header]);
    }

    /// <summary>Priority only orders packets going to the same connection, so a busy connection never holds up another.</summary>
    [Fact]
    public async Task Request_DifferentConnections_DoNotWaitForEachOther()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);
        PeerConnection other = new(new ConnectionPoint { IpAddress = "10.0.0.6", Port = 4000 }, new IpConnectionInfo(), () => { });

        Task<bool> first = fx.Transport.Request(target, Payload(5));
        await WaitUntil(() => fx.Sends.Count == 1);
        Task<bool> second = fx.Transport.Request(other, Payload(5));

        await WaitUntil(() => fx.Sends.Count == 2);
        manual.Complete(0);
        manual.Complete(1);
        Assert.True(await first);
        Assert.True(await second);
    }

    /// <summary>Once a packet fails the rest of its payload is not sent, and other payloads carry on.</summary>
    [Fact]
    public async Task Request_PacketFails_RestOfItsPayloadIsNotSent()
    {
        int calls = 0;
        Fixture fx = Build(respond: _ => Interlocked.Increment(ref calls) == 1 ? Task.FromException<bool>(new IOException("dropped")) : Task.FromResult(true));

        await Assert.ThrowsAsync<IOException>(() => fx.Transport.Request(target, Payload(35)));
        bool other = await fx.Transport.Request(target, Payload(5));

        Assert.True(other);
        Assert.Equal(2, fx.Sends.Count);
    }

    /// <summary>A payload cancelled while its packets are still queued behind others completes at once, without any of them being sent.</summary>
    [Fact]
    public async Task Request_CancelledWhileQueued_CompletesWithoutSending()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);
        using CancellationTokenSource cancellation = new();

        Task<bool> running = fx.Transport.Request(target, Payload(5));
        await WaitUntil(() => fx.Sends.Count == 1);
        Task<bool> queued = fx.Transport.Request(target, Payload(35), cancellation: cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        manual.Complete(0);
        Assert.True(await running);
        Assert.Single(fx.Sends);
    }

    /// <summary>Disposing the transport fails the packets still queued, while the one being sent is left to finish.</summary>
    [Fact]
    public async Task DisposeAsync_FailsQueuedPackets()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);

        Task<bool> running = fx.Transport.Request(target, Payload(5));
        await WaitUntil(() => fx.Sends.Count == 1);
        Task<bool> queued = fx.Transport.Request(target, Payload(5));

        await fx.Transport.DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => queued);
        manual.Complete(0);
        Assert.True(await running);
    }

    /// <summary>A request made after the transport was disposed fails instead of being queued for good.</summary>
    [Fact]
    public async Task Request_AfterDispose_Throws()
    {
        Fixture fx = Build();
        await fx.Transport.Request(target, Payload(5));
        await fx.Transport.DisposeAsync();

        await Assert.ThrowsAsync<IOException>(() => fx.Transport.Request(target, Payload(5)));
    }

    /// <summary>The request is only accepted when every packet was.</summary>
    [Fact]
    public async Task Request_AnyPacketRejected_ReturnsFalse()
    {
        Fixture fx = Build(respond: sent => Task.FromResult(sent.Data[Header] != 10));

        Assert.False(await fx.Transport.Request(target, Payload(35)));
    }

    /// <summary>A packet that could not be delivered fails the request, and the packets after it are not sent.</summary>
    [Fact]
    public async Task Request_AnyPacketThrows_Throws()
    {
        int calls = 0;
        Fixture fx = Build(respond: _ => Interlocked.Increment(ref calls) == 2 ? Task.FromException<bool>(new IOException("dropped")) : Task.FromResult(true));

        await Assert.ThrowsAsync<IOException>(() => fx.Transport.Request(target, Payload(35)));

        Assert.Equal(2, fx.Sends.Count);
    }

    /// <summary>The Transmitted callback fires once, only after the last packet has been transmitted.</summary>
    [Fact]
    public async Task Request_Transmitted_FiresOnceAfterLastPacket()
    {
        int transmitted = 0;
        Fixture fx = Build();

        await fx.Transport.Request(target, Payload(35), new PeerSendOptions { Transmitted = () => transmitted++ });

        Assert.Equal(4, fx.Sends.Count);
        for (int i = 0; i < 3; i++)
        {
            fx.Sends[i].Options!.Transmitted!();
            Assert.Equal(0, transmitted);
        }

        fx.Sends[3].Options!.Transmitted!();
        Assert.Equal(1, transmitted);
    }

    /// <summary>The packets are returned to their pool once sent, whether the request succeeded or failed.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Request_DisposesPacketsAfterSending(bool fail)
    {
        List<TrackedOwner> owners = [];
        Fixture fx = Build(new TrackingPacketizer(owners), respond: _ => fail ? Task.FromException<bool>(new IOException()) : Task.FromResult(true));

        try { await fx.Transport.Request(target, Payload(5)); }
        catch (IOException) { }

        Assert.Equal(3, owners.Count);
        Assert.All(owners, owner => Assert.True(owner.IsDisposed));
    }

    /// <summary>A payload that is too big for the packetizer fails the request without sending anything.</summary>
    [Fact]
    public async Task Request_PayloadTooBig_ThrowsWithoutSending()
    {
        Fixture fx = Build(new Packetizer(new RawPacketEngineController(100), maxPayloadSize: 10));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fx.Transport.Request(target, Payload(11)));

        Assert.Empty(fx.Sends);
    }

    /// <summary>Received packets are held back until the payload is whole, then published once, on the connection they arrived on.</summary>
    [Fact]
    public void Received_PacketsPublishedOnlyOnceComplete()
    {
        Fixture fx = Build();
        List<PeerReceivedEventArgs> published = [];
        fx.Transport.Received.Listen(published.Add);
        PeerConnection connection = Connection();
        byte[] payload = Payload(35);
        List<byte[]> packets = Packets(payload);

        for (int i = 0; i < packets.Count - 1; i++)
        {
            fx.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = packets[i] });
            Assert.Empty(published);
        }

        fx.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = packets[^1] });

        PeerReceivedEventArgs args = Assert.Single(published);
        Assert.Same(connection, args.Connection);
        Assert.Equal(payload, args.Payload.ToArray());
    }

    /// <summary>Bytes that are not a packet, such as a whole payload from a node that does not packetize, are dropped without throwing.</summary>
    [Fact]
    public void Received_NotAPacket_IsDropped()
    {
        Fixture fx = Build();
        List<PeerReceivedEventArgs> published = [];
        fx.Transport.Received.Listen(published.Add);

        fx.Received.Publish(new PeerReceivedEventArgs { Connection = Connection(), Payload = new byte[] { 0x0A, 0x05, 1, 2, 3 } });

        Assert.Empty(published);
    }

    /// <summary>Each connection reassembles on its own, so identical packet ids from two senders never mix.</summary>
    [Fact]
    public void Received_TwoConnections_AssembleSeparately()
    {
        Fixture fx = Build();
        List<PeerReceivedEventArgs> published = [];
        fx.Transport.Received.Listen(published.Add);
        PeerConnection a = Connection();
        PeerConnection b = Connection();
        byte[] payload = Payload(15);
        List<byte[]> packets = Packets(payload);

        fx.Received.Publish(new PeerReceivedEventArgs { Connection = a, Payload = packets[0] });
        fx.Received.Publish(new PeerReceivedEventArgs { Connection = b, Payload = packets[1] });
        Assert.Empty(published);

        fx.Received.Publish(new PeerReceivedEventArgs { Connection = a, Payload = packets[1] });

        PeerReceivedEventArgs args = Assert.Single(published);
        Assert.Same(a, args.Connection);
        Assert.Equal(payload, args.Payload.ToArray());
    }

    /// <summary>When a connection is lost its half-received payloads are discarded, and the loss is passed on.</summary>
    [Fact]
    public void Disconnected_DiscardsPartialPayloads_AndIsForwarded()
    {
        Fixture fx = Build();
        List<PeerReceivedEventArgs> published = [];
        List<PeerConnection> lost = [];
        fx.Transport.Received.Listen(published.Add);
        fx.Transport.Disconnected.Listen(args => lost.Add(args.Connection));
        PeerConnection connection = Connection();
        List<byte[]> packets = Packets(Payload(15));
        fx.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = packets[0] });

        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        fx.Received.Publish(new PeerReceivedEventArgs { Connection = connection, Payload = packets[1] });

        Assert.Same(connection, Assert.Single(lost));
        Assert.Empty(published);
    }

    /// <summary>A connection coming up is passed on.</summary>
    [Fact]
    public void Connected_IsForwarded()
    {
        Fixture fx = Build();
        List<PeerConnection> came = [];
        fx.Transport.Connected.Listen(args => came.Add(args.Connection));
        PeerConnection connection = Connection();

        fx.Connected.Publish(new PeerConnectionEventArgs { Connection = connection });

        Assert.Same(connection, Assert.Single(came));
    }

    /// <summary>When a connection is lost its scheduler is disposed, so packets still queued for it fail instead of waiting for a connection that is gone.</summary>
    [Fact]
    public async Task Disconnected_FailsPacketsStillQueued()
    {
        Manual manual = new();
        Fixture fx = Build(respond: manual.Respond);
        PeerConnection connection = Connection();

        Task<bool> request = fx.Transport.Request(connection, Payload(35));
        await WaitUntil(() => fx.Sends.Count == 1);
        fx.Disconnected.Publish(new PeerConnectionEventArgs { Connection = connection });
        manual.Complete(0);

        await Assert.ThrowsAsync<IOException>(() => request);
    }

    /// <summary>Everything that is not about payloads goes straight to the wrapped transport.</summary>
    [Fact]
    public async Task ManagementCalls_GoToWrappedTransport()
    {
        Fixture fx = Build();
        ConnectionPoint point = new() { IpAddress = "10.0.0.5", Port = 4000 };
        fx.Inner.Setup(t => t.Connect(point, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        fx.Transport.StartListener(50021);
        PeerConnection connected = await fx.Transport.Connect(point);
        fx.Transport.SetClosed(point, true);
        fx.Transport.Reset(point);
        await fx.Transport.DisposeAsync();

        Assert.Same(target, connected);
        fx.Inner.Verify(t => t.StartListener(50021), Times.Once);
        fx.Inner.Verify(t => t.Connect(point, It.IsAny<CancellationToken>()), Times.Once);
        fx.Inner.Verify(t => t.SetClosed(point, true), Times.Once);
        fx.Inner.Verify(t => t.Reset(point), Times.Once);
        fx.Inner.Verify(t => t.DisposeAsync(), Times.Once);
    }
}
