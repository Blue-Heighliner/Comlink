namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="PacketBuilder{TPacket}"/> and the <see cref="PacketMap"/> it produces.</summary>
public sealed class PacketBuilderTests
{
    private static PacketBuilder<TestPacket> Complete()
    {
        PacketBuilder<TestPacket> builder = new();
        builder.Frame<TestFramePacketHandler>();
        return builder;
    }

    /// <summary>Building fails and names the frame packet handler when it was not stated.</summary>
    [Fact]
    public void Build_UnstatedHandler_ThrowsNamingIt()
    {
        PacketBuilder<TestPacket> builder = new();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestPacket", error.Message);
        Assert.Contains("Frame", error.Message);
    }

    /// <summary>The frame packet handler the host states creates a packet from a piece of a payload, recognizes it, and reads each aspect back.</summary>
    [Fact]
    public void FramePacketHandler_CreatesRecognizesAndReads()
    {
        IFramePacketAdapter handler = Complete().Build().FramePacket.Create(null);

        object packet = handler.Create(new FramePacketCreateContext { PayloadId = 5, Index = 2, Count = 9, PayloadLength = 1000, Data = new byte[] { 1, 2, 3 } });

        Assert.IsType<TestPacket>(packet);
        Assert.True(handler.IsValid(packet));
        Assert.False(handler.IsValid(new TestPacket()));
        Assert.Equal((5, 2, 9, 1000), (handler.GetPayloadId(packet), handler.GetIndex(packet), handler.GetCount(packet), handler.GetPayloadLength(packet)));
        Assert.Equal(new byte[] { 1, 2, 3 }, handler.GetData(packet).ToArray());
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
        PacketBuilder<TestPacket> builder = Complete();
        builder.Serializer<IPacketSerializer>();

        PacketMap map = builder.Build();

        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
    }
}
