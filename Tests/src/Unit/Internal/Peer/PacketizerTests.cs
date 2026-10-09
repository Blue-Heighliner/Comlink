namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="Packetizer"/>.</summary>
public sealed class PacketizerTests
{
    private static int Header => RawPacketSerializer.HeaderSize;

    private static byte[] Payload(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

    private static void Release(IEnumerable<Packet> packets)
    {
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }
    }

    private sealed class FrameSpyEngineController(int payloadSize, List<object?> frames) : RawPacketEngineController(payloadSize)
    {
        public override IPacketSerializer? PacketSerializer { get; } = new FrameSpySerializer(frames);
    }

    private sealed class FrameSpySerializer(List<object?> frames) : IPacketSerializer
    {
        private readonly RawPacketSerializer raw = new();

        public IMemoryOwner<byte> Serialize(object value, object? frame)
        {
            frames.Add(frame);
            return raw.Serialize(value, frame);
        }

        public object Deserialize(ReadOnlyMemory<byte> data) => raw.Deserialize(data);
    }

    /// <summary>The original frame is handed to the packet serializer with every packet it makes.</summary>
    [Fact]
    public void Split_HandsTheOriginalFrameToThePacketSerializer()
    {
        List<object?> frames = [];
        Packetizer packetizer = new(new FrameSpyEngineController(10, frames));
        frames.Clear();
        TestFrame frame = new();

        Release(packetizer.Split(Payload(35), 0, frame));

        Assert.Equal(4, frames.Count);
        Assert.All(frames, seen => Assert.Same(frame, seen));
    }

    private sealed class ConfiguringEngineController(int payloadSize, List<(object Frame, TestPacket Packet)> configured) : RawPacketEngineController(payloadSize)
    {
        public override IFrameSerializer FrameSerializer { get; } = new ConfiguringSerializer(configured);
    }

    private sealed class ConfiguringSerializer(List<(object Frame, TestPacket Packet)> configured) : IFrameSerializer
    {
        public void ConfigurePacket(object frame, object packet) => configured.Add((frame, (TestPacket)packet));

        public IMemoryOwner<byte> Serialize(object frame) => throw new NotSupportedException();

        public object Deserialize(ReadOnlyMemory<byte> data, object? packet) => throw new NotSupportedException();
    }

    private sealed class ChangingIdsEngineController(int payloadSize) : RawPacketEngineController(payloadSize)
    {
        private int next;

        public override string GetFrameId(object value) => (next++).ToString();
    }

    /// <summary>A packet handler that gives the packets of one frame different ids is refused, since a receiver could not put them back together.</summary>
    [Fact]
    public void Split_PacketsOfOneFrameWithDifferentIds_Throws()
    {
        Packetizer packetizer = new(new ChangingIdsEngineController(10));

        Assert.Throws<InvalidOperationException>(() => packetizer.Split(Payload(35), 0, new TestFrame()));
    }

    /// <summary>A payload that fits one packet is not affected by the frame id check.</summary>
    [Fact]
    public void Split_SinglePacket_NeedsNoMatchingIds()
    {
        Packetizer packetizer = new(new ChangingIdsEngineController(100));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(35), 0, new TestFrame());

        Release(packets);
        Assert.Single(packets);
    }

    /// <summary>Every frame packet is marked as one and configured from their frame by the frame serializer, in order.</summary>
    [Fact]
    public void Split_FramePackets_AreMarkedAndConfiguredFromTheirFrame()
    {
        List<(object Frame, TestPacket Packet)> configured = [];
        Packetizer packetizer = new(new ConfiguringEngineController(10, configured));
        configured.Clear();
        TestFrame frame = new();

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(35), 0, frame);
        Release(packets);

        Assert.Equal([0, 1, 2, 3], configured.Select(entry => entry.Packet.Index));
        Assert.All(configured, entry => Assert.Same(frame, entry.Frame));
        Assert.All(configured, entry => Assert.True(entry.Packet.IsFramePacket));

    }

    /// <summary>A payload that fits in one packet becomes exactly one packet carrying the payload after the packet's fields.</summary>
    [Fact]
    public void Split_SmallPayload_IsOnePacket()
    {
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: 100));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(20), priority: 3, frame: new TestFrame());

        Packet packet = Assert.Single(packets);
        Assert.Equal(Header + 20, packet.Data.Memory.Length);
        Assert.Equal(3, packet.Priority);
        Release(packets);
    }

    /// <summary>A larger payload is cut into packets no bigger than the payload size, all inheriting the payload's priority.</summary>
    [Fact]
    public void Split_LargePayload_IsCutIntoBoundedPackets()
    {
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: 10));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(35), priority: 5, frame: new TestFrame());

        Assert.Equal(4, packets.Count);
        Assert.All(packets, packet => Assert.True(packet.Data.Memory.Length <= Header + 10));
        Assert.All(packets, packet => Assert.Equal(5, packet.Priority));
        Assert.Equal(Header + 5, packets[^1].Data.Memory.Length);
        Release(packets);
    }

    /// <summary>An empty payload still produces one packet, so a receiver can tell it from silence.</summary>
    [Fact]
    public void Split_EmptyPayload_IsOnePacket()
    {
        Packetizer packetizer = new(new RawPacketEngineController());

        IReadOnlyList<Packet> packets = packetizer.Split(ReadOnlyMemory<byte>.Empty, priority: 0, frame: new TestFrame());

        Assert.Equal(Header, Assert.Single(packets).Data.Memory.Length);
        Release(packets);
    }

    /// <summary>A payload over the payload limit is refused.</summary>
    [Fact]
    public void Split_PayloadOverLimit_Throws()
    {
        Packetizer packetizer = new(new RawPacketEngineController(), maxFrameSize: 50);

        Assert.Throws<ArgumentOutOfRangeException>(() => packetizer.Split(Payload(51), priority: 0, frame: new TestFrame()));
    }

    /// <summary>A payload that would need more packets than a receiver will track is refused rather than mis-numbered.</summary>
    [Fact]
    public void Split_MoreThanMaximumPacketCount_Throws()
    {
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => packetizer.Split(new byte[Packetizer.MaxPacketCount + 1], priority: 0, frame: new TestFrame()));
    }

    /// <summary>Each split gets its own payload id, so two payloads in flight never share one.</summary>
    [Fact]
    public void Split_TwoPayloads_UseDifferentIds()
    {
        Packetizer packetizer = new(new RawPacketEngineController());

        IReadOnlyList<Packet> first = packetizer.Split(Payload(5), 0, new TestFrame());
        IReadOnlyList<Packet> second = packetizer.Split(Payload(5), 0, new TestFrame());

        Assert.NotEqual(first[0].Data.Memory.Span[..4].ToArray(), second[0].Data.Memory.Span[..4].ToArray());
        Release(first);
        Release(second);
    }

    /// <summary>The packets carry the payload id, index, count and length through the controller's field members.</summary>
    [Fact]
    public void Split_FillsPacketFieldsThroughTheController()
    {
        RawPacketSerializer serializer = new();
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: 10));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(25), 0, new TestFrame());

        TestPacket[] decoded = [.. packets.Select(packet => (TestPacket)serializer.Deserialize(packet.Data.Memory)!)];
        Assert.Single(decoded.Select(p => p.PayloadId).Distinct());
        Assert.Equal([0, 1, 2], decoded.Select(p => p.Index));
        Assert.All(decoded, p => Assert.Equal(3, p.Count));
        Assert.All(decoded, p => Assert.Equal(25, p.PayloadLength));
        Assert.Equal([10, 10, 5], decoded.Select(p => p.Data.Length));
        Release(packets);
    }

    /// <summary>Limits too small to be usable are rejected up front.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 0)]
    public void Constructor_UnusableLimits_Throw(int maxFrameSize, int maxPendingFrames)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Packetizer(new RawPacketEngineController(), maxFrameSize, maxPendingFrames));
    }

    /// <summary>A payload size below 1 is refused when the packetizer is created.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositivePayloadSize_Throws(int payloadSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Packetizer(new RawPacketEngineController(payloadSize)));
    }

    /// <summary>An engine controller with no packet type cannot be packetized.</summary>
    [Fact]
    public void Constructor_NoPacketType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Packetizer(new TestEngineController()));
    }

    /// <summary>A payload size far beyond any payload is fine: everything goes in one packet.</summary>
    [Fact]
    public void Constructor_HugePayloadSize_Works()
    {
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: int.MaxValue), maxFrameSize: 1000);

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(500), 0, new TestFrame());

        Assert.Single(packets);
        Release(packets);
    }

    /// <summary>With the default protobuf packet serializer, a payload is cut by the payload size and reassembles.</summary>
    [Fact]
    public void SplitThenAssemble_DefaultSerializer_RoundTrips()
    {
        Packetizer packetizer = new(new TestPacketEngineController());
        Mock<TestPacketEngineController> small = new() { CallBase = true };
        small.Setup(c => c.MaxPayloadSize).Returns(400);
        packetizer = new Packetizer(small.Object);
        byte[] payload = Payload(1000);

        IReadOnlyList<Packet> packets = packetizer.Split(payload, 0, new TestFrame());

        Assert.Equal(3, packets.Count);
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        AssembledPayload? complete = null;
        foreach (Packet packet in packets)
        {
            complete = assembler.Add(packet.Data.Memory) ?? complete;
        }
        using IMemoryOwner<byte> owner = Assert.IsType<AssembledPayload>(complete).Payload;
        Assert.Equal(payload, owner.Memory.ToArray());
        Release(packets);
    }

    /// <summary>Packets produced by Split reassemble into the original payload through an assembler the same packetizer created.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(1000)]
    public void SplitThenAssemble_RoundTripsPayload(int length)
    {
        Packetizer packetizer = new(new RawPacketEngineController(payloadSize: 10));
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] payload = Payload(length);

        AssembledPayload? complete = null;
        foreach (Packet packet in packetizer.Split(payload, 0, new TestFrame()))
        {
            using (packet) { complete = assembler.Add(packet.Data.Memory) ?? complete; }
        }

        using IMemoryOwner<byte> owner = Assert.IsType<AssembledPayload>(complete).Payload;
        Assert.Equal(payload, owner.Memory.ToArray());
    }

    /// <summary>Every packet the packetizer builds is disposed once it has been serialized, if the host's packet type is disposable.</summary>
    [Fact]
    public void Split_DisposesEachPacketItBuilds()
    {
        RawPacketEngineController controller = new(payloadSize: 10);
        Packetizer packetizer = new(controller);
        RawPacketSerializer packetSerializer = (RawPacketSerializer)controller.PacketSerializer!;
        packetSerializer.Serialized.Clear();

        IReadOnlyList<Packet> packets = packetizer.Split(new byte[25], 0, new TestFrame());

        Assert.Equal(packets.Count, packetSerializer.Serialized.Count);
        Assert.All(packetSerializer.Serialized, packet => Assert.True(packet.IsDisposed));
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }
    }

    /// <summary>The packets of one frame all carry the frame id the handler gave them, so they reassemble, and a handler that gives them different ids is refused.</summary>
    [Fact]
    public void Split_PacketsOfOneFrame_ShareTheirFrameId_AndDisagreementIsRefused()
    {
        RawPacketEngineController controller = new(payloadSize: 10);
        Packetizer packetizer = new(controller);
        TestFrame frame = new();
        RawPacketSerializer packetSerializer = (RawPacketSerializer)controller.PacketSerializer!;
        packetSerializer.Serialized.Clear();

        IReadOnlyList<Packet> packets = packetizer.Split(new byte[25], 0, frame);

        Assert.Equal(packets.Count, packetSerializer.Serialized.Count(packet => packet.IsFramePacket));
        Assert.Single(packetSerializer.Serialized.Where(packet => packet.IsFramePacket).Select(packet => packet.PayloadId).Distinct());
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }

        Mock<RawPacketEngineController> disagreeing = new(10, 1) { CallBase = true };
        int next = 0;
        disagreeing.Setup(c => c.GetFrameId(It.IsAny<object>())).Returns(() => (next++).ToString());
        Packetizer refusing = new(disagreeing.Object);

        Assert.Throws<InvalidOperationException>(() => refusing.Split(new byte[25], 0, frame));
    }
}
