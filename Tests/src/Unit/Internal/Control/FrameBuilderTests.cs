namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>Unit tests for <see cref="FrameBuilder{TFrame, TPriority, TLevel, TAspect}"/> and the <see cref="FrameMap"/> it produces.</summary>
public sealed class FrameBuilderTests
{
    private static FrameBuilder<TestFrame, TestMessagePriority, TestLevel, TestAspect> Complete() => new();

    /// <summary>A frame mapping needs nothing stated: the engine knows nothing of what is in a frame.</summary>
    [Fact]
    public void Build_WithNothingStated_Succeeds() => Assert.Equal(typeof(TestFrame), Complete().Build().Type);

    /// <summary>The map creates the host's own frame type.</summary>
    [Fact]
    public void Map_CreatesTheHostsFrame() => Assert.IsType<TestFrame>(Complete().Build().Create());

    /// <summary>The default serializer builds only the host's frame type, and the default factory calls its parameterless constructor.</summary>
    [Fact]
    public void Defaults_SerializerBuildsOnlyTheMessageType_AndCreateIsNew()
    {
        FrameMap map = Complete().Build();
        IFrameSerializer serializer = map.Serializer.Create(null);
        using IMemoryOwner<byte> own = serializer.Serialize(new TestFrame { MessageId = "M" });
        using IMemoryOwner<byte> other = new ProtobufSerializer().Serialize(new TestHello { Name = "N" });

        Assert.IsType<TestFrame>(serializer.Deserialize(own.Memory, null));
        Assert.Throws<InvalidDataException>(() => serializer.Deserialize(other.Memory, null));
        Assert.IsType<TestFrame>(map.Create());
    }

    /// <summary>A serializer and a factory the host states replace the defaults.</summary>
    [Fact]
    public void Serializer_AndCreate_CanBeReplaced()
    {
        IFrameSerializer serializer = Mock.Of<IFrameSerializer>();
        TestFrame created = new() { MessageId = "CREATED" };

        FrameMap map = Complete().Serializer<IFrameSerializer>().Create(() => created) is FrameBuilder<TestFrame, TestMessagePriority, TestLevel, TestAspect> builder ? builder.Build() : throw new InvalidOperationException();

        Assert.Same(serializer, map.Serializer.Create(new ServiceCollection().AddSingleton(serializer).BuildServiceProvider()));
        Assert.Same(created, map.Create());
    }

    /// <summary>Auto forwarders are stated by name on the engine builder, once each, and the engine controller reports them.</summary>
    [Fact]
    public void AutoForwarders_AreStatedOnceEachAndReportedByTheEngineController()
    {
        EngineBuilder engine = EngineBuilder.Build(new AutoForwarderConfiguration());

        EngineController controller = new(engine, new CurrentUserProvider());

        Assert.Equal(["Alerts", "Other"], controller.AutoForwarders.Select(forwarder => forwarder.Name));
    }

    private sealed class AutoForwarderConfiguration : IEngineConfiguration
    {
        public void Configure(IEngineBuilder engine)
            => engine.Types<TestFrame, TestPacket, TestMessagePriority, TestLevel, TestAspect>()
                .Priority(TestMessagePriority.Normal)
                .AutoForwarder("Alerts").AutoForwarder("Other").AutoForwarder("Alerts")
                .Frames<TestNetworkProcessor>();
    }
}
