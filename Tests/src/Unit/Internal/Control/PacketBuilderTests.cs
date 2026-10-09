namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="PacketBuilder{TPacket, TPriority}"/> and the <see cref="PacketMap"/> it produces.</summary>
public sealed class PacketBuilderTests
{
    private static PacketBuilder<TestFrame, TestPacket, TestMessagePriority> Complete()
    {
        PacketBuilder<TestFrame, TestPacket, TestMessagePriority> builder = new();
        builder.Handler<TestPacketHandler>();
        return builder;
    }

    /// <summary>Building fails and names the packet handler when it was not stated.</summary>
    [Fact]
    public void Build_UnstatedHandler_ThrowsNamingIt()
    {
        PacketBuilder<TestFrame, TestPacket, TestMessagePriority> builder = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestPacket", error.Message);
        Assert.Contains("Handler", error.Message);
    }

    /// <summary>The packet handler the host states creates a packet from a piece of a frame, recognizes it, and reads each aspect back.</summary>
    [Fact]
    public void PacketHandler_CreatesRecognizesAndReads()
    {
        IPacketAdapter handler = Complete().Build().Handler.Create(null);

        TestFrame frame = new();
        object packet = handler.CreateFramePacket(frame, 2, 9, 1000, new byte[] { 1, 2, 3 });

        Assert.IsType<TestPacket>(packet);
        Assert.True(handler.IsFramePacket(packet));
        Assert.False(handler.IsFramePacket(new TestPacket()));
        Assert.Equal((2, 9, 1000), (handler.GetIndex(packet), handler.GetCount(packet), handler.GetFrameLength(packet)));
        Assert.Equal(handler.GetFrameId(packet), handler.GetFrameId(handler.CreateFramePacket(frame, 3, 9, 1000, new byte[] { 9 })));
        Assert.Equal(new byte[] { 1, 2, 3 }, handler.GetPayload(packet).ToArray());
    }

    /// <summary>The default serializer builds only the packet type.</summary>
    [Fact]
    public void Defaults_Serializer()
    {
        PacketMap map = Complete().Build();
        IPacketSerializer serializer = map.Serializer.Create(null);
        using IMemoryOwner<byte> own = serializer.Serialize(new TestPacket { Count = 1 }, null);
        using IMemoryOwner<byte> other = new ProtobufSerializer().Serialize(new TestFrame());

        Assert.IsType<TestPacket>(serializer.Deserialize(own.Memory));
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(other.Memory));
    }

    /// <summary>The serializer a host states replaces the default.</summary>
    [Fact]
    public void Serializer_CanBeReplaced()
    {
        IPacketSerializer serializer = Mock.Of<IPacketSerializer>();
        PacketBuilder<TestFrame, TestPacket, TestMessagePriority> builder = Complete();
        builder.Serializer<IPacketSerializer>();

        PacketMap map = builder.Build();

        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
    }
}
