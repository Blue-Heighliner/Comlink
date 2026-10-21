namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IDebugMode"/> the engine offers, whose answer a test chooses.</summary>
public sealed class MockDebugMode : Mock<IDebugMode>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="isDebug">What <see cref="IDebugMode.IsDebug"/> returns.</param>
    public MockDebugMode(bool isDebug = false)
    {
        IsDebug = isDebug;
        Setup(mode => mode.IsDebug).Returns(() => IsDebug);
    }

    /// <summary>Gets or sets what <see cref="IDebugMode.IsDebug"/> returns.</summary>
    public bool IsDebug { get; set; }
}
