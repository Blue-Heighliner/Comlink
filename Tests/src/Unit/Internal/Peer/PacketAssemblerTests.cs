namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Peer;

/// <summary>Unit tests for <see cref="PacketAssembler"/>, driven by packets the <see cref="Packetizer"/> made or hand-built ones.</summary>
public sealed class PacketAssemblerTests
{
    private static readonly RawPacketSerializer serializer = new();
    private static int Header => RawPacketSerializer.HeaderSize;

    private static byte[] Payload(int length, byte seed = 0) => [.. Enumerable.Range(0, length).Select(i => (byte)((i + seed) % 251))];

    private static Packetizer Build(int maxPendingPayloads = 32, int maxPayloadSize = 64 * 1024 * 1024)
        => new(new RawPacketEngineController(packetSize: Header + 10), maxPayloadSize, maxPendingPayloads);

    private static List<byte[]> Packets(Packetizer packetizer, byte[] payload)
    {
        IReadOnlyList<Packet> packets = packetizer.Split(payload, 0);
        List<byte[]> bytes = [.. packets.Select(p => p.Data.Memory.ToArray())];
        foreach (Packet packet in packets)
        {
            packet.Dispose();
        }
        return bytes;
    }

    private static byte[] Wire(int id, int index, int count, int total, byte[] data)
    {
        using IMemoryOwner<byte> owner = serializer.Serialize(new TestPacket { IsFramePacket = true, PayloadId = id, Index = index, Count = count, PayloadLength = total, Data = data }, null);
        return owner.Memory.ToArray();
    }

    private static byte[]? Add(IPacketAssembler assembler, byte[] packet)
    {
        AssembledPayload? complete = assembler.Add(packet);
        using IMemoryOwner<byte>? payload = complete?.Payload;
        return payload?.Memory.ToArray();
    }

    /// <summary>A heartbeat packet is discarded instead of being assembled or rejected.</summary>
    [Fact]
    public void Add_HeartbeatPacket_IsDiscarded()
    {
        EngineController controller = new(EngineBuilder.Build(new TestEngineConfiguration(packetExtra: packet => packet.Heartbeat<TestPacketHeartbeatHandler>())), new CurrentUserProvider(), null);
        using IPacketAssembler assembler = new Packetizer(controller).CreateAssembler();
        using IMemoryOwner<byte> heartbeat = controller.PacketSerializer!.Serialize(controller.CreatePacketHeartbeat(), null);

        Assert.Null(assembler.Add(heartbeat.Memory));
    }

    /// <summary>Packets that arrive in reverse order still reassemble the payload, and it completes only on the last one.</summary>
    [Fact]
    public void Add_PacketsOutOfOrder_AssembleOnLast()
    {
        Packetizer packetizer = Build();
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] payload = Payload(35);
        List<byte[]> packets = Packets(packetizer, payload);
        packets.Reverse();

        for (int i = 0; i < packets.Count - 1; i++) { Assert.Null(Add(assembler, packets[i])); }

        Assert.Equal(payload, Add(assembler, packets[^1]));
    }

    /// <summary>The completed payload comes with the packet at index zero, whichever order the packets arrived in.</summary>
    [Fact]
    public void Add_Completion_CarriesTheFirstPacket()
    {
        Packetizer packetizer = Build();
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        List<byte[]> packets = Packets(packetizer, Payload(35));
        packets.Reverse();

        AssembledPayload? complete = null;
        foreach (byte[] packet in packets)
        {
            complete = assembler.Add(packet) ?? complete;
        }

        using IMemoryOwner<byte> payload = Assert.IsType<AssembledPayload>(complete).Payload;
        Assert.Equal(0, Assert.IsType<TestPacket>(complete.FirstPacket).Index);
    }

    /// <summary>Packets of two payloads interleaved on one connection each complete their own payload.</summary>
    [Fact]
    public void Add_InterleavedPayloads_EachAssemblesSeparately()
    {
        Packetizer packetizer = Build();
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] first = Payload(25, seed: 1);
        byte[] second = Payload(25, seed: 100);
        List<byte[]> a = Packets(packetizer, first);
        List<byte[]> b = Packets(packetizer, second);

        Assert.Null(Add(assembler, a[0]));
        Assert.Null(Add(assembler, b[0]));
        Assert.Null(Add(assembler, a[1]));
        Assert.Null(Add(assembler, b[1]));
        Assert.Equal(first, Add(assembler, a[2]));
        Assert.Equal(second, Add(assembler, b[2]));
    }

    /// <summary>A packet delivered twice is counted once, so it cannot complete a payload that is still missing a packet.</summary>
    [Fact]
    public void Add_DuplicatePacket_IsIgnored()
    {
        Packetizer packetizer = Build();
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] payload = Payload(25);
        List<byte[]> packets = Packets(packetizer, payload);

        Assert.Null(Add(assembler, packets[0]));
        Assert.Null(Add(assembler, packets[0]));
        Assert.Null(Add(assembler, packets[1]));
        Assert.Equal(payload, Add(assembler, packets[2]));
    }

    /// <summary>Once a payload completes its state is released, so the same packets can be assembled again.</summary>
    [Fact]
    public void Add_AfterCompletion_StartsFresh()
    {
        Packetizer packetizer = Build();
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        List<byte[]> packets = Packets(packetizer, Payload(15));

        Add(assembler, packets[0]);
        Assert.NotNull(Add(assembler, packets[1]));

        Assert.Null(Add(assembler, packets[0]));
    }

    /// <summary>Bytes the packet serializer cannot read as a packet are rejected.</summary>
    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void Add_NotAPacket_Throws(byte[] bytes)
    {
        using IPacketAssembler assembler = Build().CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(bytes));
    }

    /// <summary>Bytes that deserialize to something other than the packet type are rejected.</summary>
    [Fact]
    public void Add_WrongType_Throws()
    {
        using IPacketAssembler assembler = new Packetizer(new TestPacketEngineController()).CreateAssembler();
        using IMemoryOwner<byte> message = new ProtobufSerializer().Serialize(new TestFrame { MessageId = "M1" });

        Assert.Throws<InvalidDataException>(() => assembler.Add(message.Memory.ToArray()));
    }

    /// <summary>Packets whose fields contradict each other or the limits are rejected.</summary>
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(0, 3, 3, 15)]
    [InlineData(0, -1, 3, 15)]
    [InlineData(0, 0, 2, 3)]
    [InlineData(0, 0, 2, -1)]
    [InlineData(0, 0, 70000, 15)]
    public void Add_InconsistentFields_Throws(int id, int index, int count, int total)
    {
        using IPacketAssembler assembler = Build().CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(id, index, count, total, Payload(10))));
    }

    /// <summary>A packet announcing more than the payload limit is rejected before anything is allocated for it.</summary>
    [Fact]
    public void Add_PayloadOverLimit_Throws()
    {
        using IPacketAssembler assembler = Build(maxPayloadSize: 20).CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 0, 3, 25, Payload(10))));
    }

    /// <summary>A chunk other than the last must not be empty, since it could then never place anything.</summary>
    [Fact]
    public void Add_EmptyChunkBeforeTheLast_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 0, 2, 5, [])));
    }

    /// <summary>A packet whose count or length disagrees with the earlier packets of its payload is rejected.</summary>
    [Fact]
    public void Add_ContradictsEarlierPackets_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();
        Add(assembler, Wire(1, 0, 3, 25, Payload(10)));

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 1, 4, 25, Payload(10))));
        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 1, 3, 26, Payload(10))));
    }

    /// <summary>A single-packet payload whose data is not its whole length is rejected.</summary>
    [Fact]
    public void Add_SinglePacketNotItsWholePayload_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 0, 1, 30, Payload(20))));
    }

    /// <summary>Chunks of one payload that are not all the same size, last excepted, are rejected.</summary>
    [Fact]
    public void Add_ChunksDifferInSize_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();
        Add(assembler, Wire(1, 0, 4, 40, Payload(10)));

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 1, 4, 40, Payload(8))));
    }

    /// <summary>A chunk that would land past the end of its payload is rejected.</summary>
    [Fact]
    public void Add_ChunkBeyondTheEnd_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 2, 4, 15, Payload(10))));
    }

    /// <summary>Chunk lengths that do not add up to the payload length are rejected once the last packet arrives, rather than yielding a corrupt payload.</summary>
    [Fact]
    public void Add_ChunkLengthsDoNotAddUp_Throws()
    {
        using IPacketAssembler assembler = Build().CreateAssembler();
        Add(assembler, Wire(1, 0, 2, 15, Payload(10)));

        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(1, 1, 2, 15, Payload(3))));
    }

    /// <summary>When more payloads are pending than allowed the oldest is dropped, so its later packets can no longer complete it.</summary>
    [Fact]
    public void Add_TooManyPending_DropsOldest()
    {
        Packetizer packetizer = Build(maxPendingPayloads: 2);
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] second = Payload(15, seed: 50);
        List<byte[]> a = Packets(packetizer, Payload(15));
        List<byte[]> b = Packets(packetizer, second);
        List<byte[]> c = Packets(packetizer, Payload(15, seed: 99));

        Assert.Null(Add(assembler, a[0]));
        Assert.Null(Add(assembler, b[0]));
        Assert.Null(Add(assembler, c[0]));

        Assert.Equal(second, Add(assembler, b[1]));
        Assert.Null(Add(assembler, a[1]));
    }

    /// <summary>Payloads that together hold more than twice the payload limit push out the oldest, so what a connection can make the assembler hold is bounded by what it has actually sent.</summary>
    [Fact]
    public void Add_PendingBytesOverBudget_DropsOldest()
    {
        using IPacketAssembler assembler = Build(maxPayloadSize: 40).CreateAssembler();
        byte[] second = Payload(40, seed: 50);
        byte[][] a = [.. Enumerable.Range(0, 4).Select(i => Wire(1, i, 4, 40, Payload(10)))];
        byte[][] b = [.. Enumerable.Range(0, 4).Select(i => Wire(2, i, 4, 40, second[(i * 10)..((i * 10) + 10)]))];
        byte[][] c = [.. Enumerable.Range(0, 4).Select(i => Wire(3, i, 4, 40, Payload(10, seed: 99)))];

        for (int i = 0; i < 3; i++) { Assert.Null(Add(assembler, a[i])); }
        for (int i = 0; i < 3; i++) { Assert.Null(Add(assembler, b[i])); }
        for (int i = 0; i < 3; i++) { Assert.Null(Add(assembler, c[i])); }

        Assert.Equal(second, Add(assembler, b[3]));
        Assert.Null(Add(assembler, a[3]));
    }

    /// <summary>A packet that turns out to be invalid does not take up one of the pending slots and push out a payload that is really arriving.</summary>
    [Fact]
    public void Add_InvalidFirstPacket_DoesNotEvictPendingPayloads()
    {
        Packetizer packetizer = Build(maxPendingPayloads: 1);
        using IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] payload = Payload(15);
        List<byte[]> packets = Packets(packetizer, payload);

        Assert.Null(Add(assembler, packets[0]));
        Assert.Throws<InvalidDataException>(() => assembler.Add(Wire(77, 2, 4, 15, Payload(10))));

        Assert.Equal(payload, Add(assembler, packets[1]));
    }

    /// <summary>Adding to a disposed assembler throws.</summary>
    [Fact]
    public void Add_AfterDispose_Throws()
    {
        Packetizer packetizer = Build();
        IPacketAssembler assembler = packetizer.CreateAssembler();
        byte[] packet = Packets(packetizer, Payload(5))[0];
        assembler.Dispose();
        assembler.Dispose();

        Assert.Throws<ObjectDisposedException>(() => assembler.Add(packet));
    }

    /// <summary>A packet that is not the first of its payload is disposed as soon as its data is copied out, and the first is kept for the frame and left to its consumer.</summary>
    [Fact]
    public void Add_DisposesEveryPacketButTheFirst()
    {
        RawPacketEngineController controller = new(packetSize: Header + 10);
        Packetizer packetizer = new(controller);
        List<byte[]> packets = Packets(packetizer, Payload(25));
        RawPacketSerializer packetSerializer = (RawPacketSerializer)controller.PacketSerializer!;
        using IPacketAssembler assembler = packetizer.CreateAssembler();

        foreach (byte[] packet in packets)
        {
            assembler.Add(packet);
        }

        List<TestPacket> decoded = [.. packetSerializer.Deserialized];
        Assert.False(decoded[0].IsDisposed);
        Assert.All(decoded.Skip(1), packet => Assert.True(packet.IsDisposed));
    }

    /// <summary>The first packet of a payload that never completes is disposed with the assembler, and a repeated packet is disposed at once.</summary>
    [Fact]
    public void Add_DisposesAHeldFirstPacketWithTheAssembler_AndARepeatAtOnce()
    {
        RawPacketEngineController controller = new(packetSize: Header + 10);
        Packetizer packetizer = new(controller);
        List<byte[]> packets = Packets(packetizer, Payload(25));
        RawPacketSerializer packetSerializer = (RawPacketSerializer)controller.PacketSerializer!;
        IPacketAssembler assembler = packetizer.CreateAssembler();

        assembler.Add(packets[0]);
        assembler.Add(packets[0]);

        List<TestPacket> decoded = [.. packetSerializer.Deserialized];
        Assert.False(decoded[0].IsDisposed);
        Assert.True(decoded[1].IsDisposed);

        assembler.Dispose();

        Assert.True(decoded[0].IsDisposed);
    }
}
