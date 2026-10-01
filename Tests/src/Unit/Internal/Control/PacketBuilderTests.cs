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

    /// <summary>The default size is 16 KiB, the default window is 1, and the default serializer builds only the packet type.</summary>
    [Fact]
    public void Defaults_SizeWindowAndSerializer()
    {
        PacketMap map = Complete().Build();
        IPacketSerializer serializer = map.Serializer.Create(null);
        using IMemoryOwner<byte> own = serializer.Serialize(new TestPacket { Count = 1 }, null);
        using IMemoryOwner<byte> other = new ProtobufSerializer().Serialize(new TestFrame());

        Assert.Equal(16 * 1024, map.Size);
        Assert.Equal(1, map.Window);
        Assert.IsType<TestPacket>(serializer.Deserialize(own.Memory));
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(other.Memory));
    }

    /// <summary>The size, window and serializer a host states replace the defaults.</summary>
    [Fact]
    public void Size_Window_AndSerializer_CanBeReplaced()
    {
        IPacketSerializer serializer = Mock.Of<IPacketSerializer>();
        PacketBuilder<TestPacket> builder = Complete();
        builder.Size(200).Window(3).Serializer<IPacketSerializer>();

        PacketMap map = builder.Build();

        Assert.Equal(200, map.Size);
        Assert.Equal(3, map.Window);
        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
    }
}
