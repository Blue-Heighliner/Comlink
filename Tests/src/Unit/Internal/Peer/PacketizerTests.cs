namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="Packetizer"/>.</summary>
public sealed class PacketizerTests
{
    private static int Header => RawPacketSerializer.HeaderSize;

    private sealed class HexPacketEngineController(int packetSize) : RawPacketEngineController(packetSize)
    {
        public override IPacketSerializer? PacketSerializer { get; } = new HexPacketSerializer();
    }

    private sealed class HexPacketSerializer : IPacketSerializer
    {
        private readonly RawPacketSerializer raw = new();

        public IMemoryOwner<byte> Serialize(object value, object? frame)
        {
            using IMemoryOwner<byte> bytes = raw.Serialize(value, frame);
            return new Owner(Encoding.ASCII.GetBytes(Convert.ToHexString(bytes.Memory.Span)));
        }

        public object Deserialize(ReadOnlyMemory<byte> data) => raw.Deserialize(Convert.FromHexString(Encoding.ASCII.GetString(data.Span)));
    }

    private sealed class BloatingPacketEngineController(int packetSize) : RawPacketEngineController(packetSize)
    {
        public override IPacketSerializer? PacketSerializer { get; } = new BloatingPacketSerializer();
    }

    private sealed class BloatingPacketSerializer : IPacketSerializer
    {
        private readonly RawPacketSerializer raw = new();

        public IMemoryOwner<byte> Serialize(object value, object? frame)
        {
            using IMemoryOwner<byte> bytes = raw.Serialize(value, frame);
            return new Owner([.. bytes.Memory.ToArray(), .. new byte[((TestPacket)value).Index == 1 ? 100 : 0]]);
        }

        public object Deserialize(ReadOnlyMemory<byte> data) => raw.Deserialize(data);
    }

    private sealed class Owner(byte[] bytes) : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = bytes;
        public void Dispose() { }
    }

    private static byte[] Payload(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

    private static void Release(IEnumerable<Packet> packets)
    {
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }
    }

    private sealed class FrameSpyEngineController(int packetSize, List<object?> frames) : RawPacketEngineController(packetSize)
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
        Packetizer packetizer = new(new FrameSpyEngineController(Header + 10, frames));
        frames.Clear();
        object frame = new();

        Release(packetizer.Split(Payload(35), 0, frame));

        Assert.Equal(4, frames.Count);
        Assert.All(frames, seen => Assert.Same(frame, seen));
    }

    private sealed class ConfiguringEngineController(int packetSize, List<(object Frame, TestPacket Packet)> configured) : RawPacketEngineController(packetSize)
    {
        public override IFrameSerializer FrameSerializer { get; } = new ConfiguringSerializer(configured);
    }

    private sealed class ConfiguringSerializer(List<(object Frame, TestPacket Packet)> configured) : IFrameSerializer
    {
        public void ConfigurePacket(object frame, object packet) => configured.Add((frame, (TestPacket)packet));

        public IMemoryOwner<byte> Serialize(object frame) => throw new NotSupportedException();

        public object Deserialize(ReadOnlyMemory<byte> data, object? packet) => throw new NotSupportedException();
    }

    /// <summary>Every frame packet is marked as one and configured from its frame by the frame serializer, in order; with no frame there is nothing to configure from.</summary>
    [Fact]
    public void Split_FramePackets_AreMarkedAndConfiguredFromTheirFrame()
    {
        List<(object Frame, TestPacket Packet)> configured = [];
        Packetizer packetizer = new(new ConfiguringEngineController(Header + 10, configured));
        object frame = new();

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(35), 0, frame);
        Release(packets);

        Assert.Equal([0, 1, 2, 3], configured.Select(entry => entry.Packet.Index));
        Assert.All(configured, entry => Assert.Same(frame, entry.Frame));
        Assert.All(configured, entry => Assert.True(entry.Packet.IsFramePacket));

        configured.Clear();
        Release(packetizer.Split(Payload(35), 0));
        Assert.Empty(configured);
    }

    /// <summary>A payload that fits in one packet becomes exactly one packet carrying the payload after the packet's fields.</summary>
    [Fact]
    public void Split_SmallPayload_IsOnePacket()
    {
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: 100));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(20), priority: 3);

        Packet packet = Assert.Single(packets);
        Assert.Equal(Header + 20, packet.Data.Memory.Length);
        Assert.Equal(3, packet.Priority);
        Release(packets);
    }

    /// <summary>A larger payload is cut into packets no bigger than the packet size, all inheriting the payload's priority.</summary>
    [Fact]
    public void Split_LargePayload_IsCutIntoBoundedPackets()
    {
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: Header + 10));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(35), priority: 5);

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

        IReadOnlyList<Packet> packets = packetizer.Split(ReadOnlyMemory<byte>.Empty, priority: 0);

        Assert.Equal(Header, Assert.Single(packets).Data.Memory.Length);
        Release(packets);
    }

    /// <summary>A payload over the payload limit is refused.</summary>
    [Fact]
    public void Split_PayloadOverLimit_Throws()
    {
        Packetizer packetizer = new(new RawPacketEngineController(), maxPayloadSize: 50);

        Assert.Throws<ArgumentOutOfRangeException>(() => packetizer.Split(Payload(51), priority: 0));
    }

    /// <summary>A payload that would need more packets than a receiver will track is refused rather than mis-numbered.</summary>
    [Fact]
    public void Split_MoreThanMaximumPacketCount_Throws()
    {
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: Header + 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => packetizer.Split(new byte[Packetizer.MaxPacketCount + 1], priority: 0));
    }

    /// <summary>Each split gets its own payload id, so two payloads in flight never share one.</summary>
    [Fact]
    public void Split_TwoPayloads_UseDifferentIds()
    {
        Packetizer packetizer = new(new RawPacketEngineController());

        IReadOnlyList<Packet> first = packetizer.Split(Payload(5), 0);
        IReadOnlyList<Packet> second = packetizer.Split(Payload(5), 0);

        Assert.NotEqual(first[0].Data.Memory.Span[..4].ToArray(), second[0].Data.Memory.Span[..4].ToArray());
        Release(first);
        Release(second);
    }

    /// <summary>The packets carry the payload id, index, count and length through the controller's field members.</summary>
    [Fact]
    public void Split_FillsPacketFieldsThroughTheController()
    {
        RawPacketSerializer serializer = new();
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: Header + 10));

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(25), 0);

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
    public void Constructor_UnusableLimits_Throw(int maxPayloadSize, int maxPendingPayloads)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Packetizer(new RawPacketEngineController(), maxPayloadSize, maxPendingPayloads));
    }

    /// <summary>A packet size with no room for payload beside the packet's own fields is refused when the packetizer is created.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    public void Constructor_PacketSizeLeavesNoRoom_Throws(int packetSize)
    {
        Assert.Throws<InvalidOperationException>(() => new Packetizer(new RawPacketEngineController(packetSize)));
    }

    /// <summary>An engine controller with no packet type cannot be packetized.</summary>
    [Fact]
    public void Constructor_NoPacketType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new Packetizer(new TestEngineController()));
    }

    /// <summary>A packet format that makes the data bigger than it is still ends up under the packet size, because how much fits is measured.</summary>
    [Fact]
    public void Split_FormatThatGrowsTheData_StaysUnderPacketSize()
    {
        HexPacketEngineController controller = new(packetSize: 200);
        Packetizer packetizer = new(controller);
        byte[] payload = Payload(500);

        IReadOnlyList<Packet> packets = packetizer.Split(payload, 0);

        Assert.All(packets, packet => Assert.True(packet.Data.Memory.Length <= 200));
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

    /// <summary>A packet size far beyond any payload is fine, and does not overflow the search for how much data fits.</summary>
    [Fact]
    public void Constructor_HugePacketSize_Works()
    {
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: int.MaxValue), maxPayloadSize: 1000);

        IReadOnlyList<Packet> packets = packetizer.Split(Payload(500), 0);

        Assert.Single(packets);
        Release(packets);
    }

    /// <summary>A serialized packet that still comes out over the packet size fails the split, and the packets already made are released.</summary>
    [Fact]
    public void Split_PacketOverPacketSize_Throws()
    {
        Packetizer packetizer = new(new BloatingPacketEngineController(packetSize: Header + 10));

        Assert.Throws<InvalidOperationException>(() => packetizer.Split(Payload(35), 0));
    }

    /// <summary>With the default protobuf packet serializer, packets stay under the packet size and reassemble.</summary>
    [Fact]
    public void SplitThenAssemble_DefaultSerializer_RoundTrips()
    {
        Packetizer packetizer = new(new TestPacketEngineController());
        Mock<TestPacketEngineController> small = new() { CallBase = true };
        small.Setup(c => c.PacketSize).Returns(400);
        packetizer = new Packetizer(small.Object);
        byte[] payload = Payload(1000);

        IReadOnlyList<Packet> packets = packetizer.Split(payload, 0);

        Assert.True(packets.Count > 2);
        Assert.All(packets, packet => Assert.True(packet.Data.Memory.Length <= 400));
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
        Packetizer packetizer = new(new RawPacketEngineController(packetSize: Header + 10));
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] payload = Payload(length);

        AssembledPayload? complete = null;
        foreach (Packet packet in packetizer.Split(payload, 0))
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
        RawPacketEngineController controller = new(packetSize: Header + 10);
        Packetizer packetizer = new(controller);
        RawPacketSerializer packetSerializer = (RawPacketSerializer)controller.PacketSerializer!;
        packetSerializer.Serialized.Clear();

        IReadOnlyList<Packet> packets = packetizer.Split(new byte[25], 0);

        Assert.Equal(packets.Count, packetSerializer.Serialized.Count);
        Assert.All(packetSerializer.Serialized, packet => Assert.True(packet.IsDisposed));
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }
    }
}
