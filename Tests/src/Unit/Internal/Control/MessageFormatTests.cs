namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Control;

/// <summary>
/// Unit tests for <see cref="EngineController"/>'s delegation of the
/// <see cref="IEngineController"/> message fields to the mapping stated by the host, using <see cref="TestEngineController"/>/<see cref="TestFrame"/>
/// as the concrete pair.
/// </summary>
public sealed class MessageFormatTests
{
    private readonly IEngineController format = new TestEngineController();

    /// <summary>FrameType reflects the generic type argument.</summary>
    [Fact]
    public void MessageType_ReflectsGenericArgument()
    {
        Assert.Equal(typeof(TestFrame), format.FrameType);
    }

    /// <summary>CreateFrame produces a new, distinct instance of the concrete frame type each time.</summary>
    [Fact]
    public void CreateMessage_ProducesDistinctInstances()
    {
        object first = format.CreateFrame();
        object second = format.CreateFrame();

        Assert.IsType<TestFrame>(first);
        Assert.IsType<TestFrame>(second);
        Assert.NotSame(first, second);
    }

}
