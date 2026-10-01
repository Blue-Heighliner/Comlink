namespace BlueHeighliner.Comlink.Tests.Unit.Public.Serialization;

/// <summary>Unit tests for <see cref="FrameSerializer{TFrame, TPacket}"/> and <see cref="PacketSerializer{TFrame, TPacket}"/>, which present the typed members through the object-typed interfaces.</summary>
public sealed class SerializerBaseTests
{
    private sealed class Owner : IMemoryOwner<byte>
    {
        public Memory<byte> Memory { get; } = new byte[1];

        public void Dispose()
        {
        }
    }

    private sealed class FrameSpy : FrameSerializer<TestFrame, TestPacket>
    {
        public TestPacket? SeenPacket { get; private set; }

        public (TestFrame Frame, TestPacket Packet)? Configured { get; private set; }

        public override void ConfigurePacket(TestFrame frame, TestPacket packet) => Configured = (frame, packet);

        public override IMemoryOwner<byte> Serialize(TestFrame frame) => new Owner();

        public override TestFrame Deserialize(ReadOnlyMemory<byte> data, TestPacket? packet)
        {
            SeenPacket = packet;
            return new TestFrame { MessageId = "M" };
        }
    }

    private sealed class PacketSpy : PacketSerializer<TestFrame, TestPacket>
    {
        public TestFrame? SeenFrame { get; private set; }

        public override IMemoryOwner<byte> Serialize(TestPacket packet, TestFrame? frame)
        {
            SeenFrame = frame;
            return new Owner();
        }

        public override TestPacket Deserialize(ReadOnlyMemory<byte> data) => new() { Count = 1 };
    }

    /// <summary>The frame serializer hands the typed packet through from the object-typed interface, and a null packet stays null.</summary>
    [Fact]
    public void FrameSerializer_PassesTheTypedPacket()
    {
        FrameSpy spy = new();
        IFrameSerializer serializer = spy;
        TestPacket packet = new();

        Assert.IsType<TestFrame>(serializer.Deserialize(new byte[1], packet));
        Assert.Same(packet, spy.SeenPacket);
        serializer.Deserialize(new byte[1], null);
        Assert.Null(spy.SeenPacket);
        using IMemoryOwner<byte> bytes = serializer.Serialize(new TestFrame());
        Assert.Single(bytes.Memory.ToArray());
    }

    /// <summary>The typed ConfigurePacket override is reached through the object-typed interface.</summary>
    [Fact]
    public void FrameSerializer_ConfigurePacket_ReachesTheTypedOverride()
    {
        FrameSpy spy = new();
        TestFrame frame = new();
        TestPacket packet = new();

        ((IFrameSerializer)spy).ConfigurePacket(frame, packet);

        Assert.Same(frame, spy.Configured!.Value.Frame);
        Assert.Same(packet, spy.Configured.Value.Packet);
    }

    /// <summary>The packet serializer hands the typed frame through from the object-typed interface, and a null frame stays null.</summary>
    [Fact]
    public void PacketSerializer_PassesTheTypedFrame()
    {
        PacketSpy spy = new();
        IPacketSerializer serializer = spy;
        TestFrame frame = new();

        using (serializer.Serialize(new TestPacket(), frame)) { }
        Assert.Same(frame, spy.SeenFrame);
        using (serializer.Serialize(new TestPacket(), null)) { }
        Assert.Null(spy.SeenFrame);
        Assert.Equal(1, Assert.IsType<TestPacket>(serializer.Deserialize(new byte[1])).Count);
    }
}
