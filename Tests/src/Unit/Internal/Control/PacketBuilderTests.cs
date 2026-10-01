namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="PacketBuilder{TPacket}"/> and the <see cref="PacketMap"/> it produces.</summary>
public sealed class PacketBuilderTests
{
    private static PacketBuilder<TestPacket> Complete()
    {
        PacketBuilder<TestPacket> builder = new();
        builder
            .PayloadId(p => p.PayloadId, (p, v) => p.PayloadId = v)
            .Index(p => p.Index, (p, v) => p.Index = v)
            .Count(p => p.Count, (p, v) => p.Count = v)
            .PayloadLength(p => p.PayloadLength, (p, v) => p.PayloadLength = v)
            .Data(p => p.Data, (p, v) => p.Data = v.ToArray());
        return builder;
    }

    /// <summary>Building fails and names every packet field that was not mapped.</summary>
    [Fact]
    public void Build_UnmappedFields_ThrowsNamingThem()
    {
        PacketBuilder<TestPacket> builder = new();
        builder.PayloadId(p => p.PayloadId, (p, v) => p.PayloadId = v);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("TestPacket", error.Message);
        Assert.Contains("Index", error.Message);
        Assert.Contains("Count", error.Message);
        Assert.Contains("PayloadLength", error.Message);
        Assert.Contains("Data", error.Message);
    }

    /// <summary>The map reads and writes each packet field through the object-typed accessors.</summary>
    [Fact]
    public void Map_ReadsAndWritesEveryField()
    {
        PacketMap map = Complete().Build();
        object packet = map.Create();

        map.SetPayloadId(packet, 5);
        map.SetIndex(packet, 2);
        map.SetCount(packet, 9);
        map.SetPayloadLength(packet, 1000);
        map.SetData(packet, new byte[] { 1, 2, 3 });

        Assert.IsType<TestPacket>(packet);
        Assert.Equal(5, map.GetPayloadId(packet));
        Assert.Equal(2, map.GetIndex(packet));
        Assert.Equal(9, map.GetCount(packet));
        Assert.Equal(1000, map.GetPayloadLength(packet));
        Assert.Equal(new byte[] { 1, 2, 3 }, map.GetData(packet).ToArray());
        Assert.Equal(typeof(TestPacket), map.Type);
    }

    /// <summary>The default size is 16 KiB, the default window is 1, and the default serializer builds only the packet type.</summary>
    [Fact]
    public void Defaults_SizeWindowAndSerializer()
    {
        PacketMap map = Complete().Build();
        INetworkSerializer serializer = map.Serializer.Create(null);
        using IMemoryOwner<byte> own = serializer.Serialize(new TestPacket { Count = 1 });
        using IMemoryOwner<byte> other = new ProtobufNetworkSerializer().Serialize(new TestMessage());

        Assert.Equal(16 * 1024, map.Size);
        Assert.Equal(1, map.Window);
        Assert.IsType<TestPacket>(serializer.Deserialize(own.Memory));
        Assert.Null(serializer.Deserialize(other.Memory));
    }

    /// <summary>The size, window, serializer and factory a host states replace the defaults.</summary>
    [Fact]
    public void Size_Window_Serializer_AndCreate_CanBeReplaced()
    {
        INetworkSerializer serializer = Mock.Of<INetworkSerializer>();
        TestPacket created = new() { Count = 42 };
        PacketBuilder<TestPacket> builder = Complete();
        builder.Size(200).Window(3).Serializer<INetworkSerializer>().Create(() => created);

        PacketMap map = builder.Build();

        Assert.Equal(200, map.Size);
        Assert.Equal(3, map.Window);
        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
        Assert.Same(created, map.Create());
    }

    /// <summary>The integer fields can be mapped by naming the property alone, while the data keeps its explicit getter and setter.</summary>
    [Fact]
    public void PropertyOverloads_MapEachMatchingFieldWithoutASetter()
    {
        PacketBuilder<TestPacket> builder = new();
        builder.PayloadId(p => p.PayloadId).Index(p => p.Index).Count(p => p.Count).PayloadLength(p => p.PayloadLength)
            .Data(p => p.Data, (p, v) => p.Data = v.ToArray());
        PacketMap map = builder.Build();
        object packet = map.Create();

        map.SetPayloadId(packet, 1);
        map.SetIndex(packet, 2);
        map.SetCount(packet, 3);
        map.SetPayloadLength(packet, 4);

        TestPacket typed = Assert.IsType<TestPacket>(packet);
        Assert.Equal((1, 2, 3, 4), (typed.PayloadId, typed.Index, typed.Count, typed.PayloadLength));
        Assert.Equal(3, map.GetCount(packet));
    }
}
