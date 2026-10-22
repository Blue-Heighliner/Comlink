namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDebugMode"/> service that says what it is told.</summary>
/// <param name="isDebug">Whether the application is in debug mode.</param>
public sealed class FakeDebugMode(bool isDebug = false) : IDebugMode
{
    /// <inheritdoc />
    public bool IsDebug { get; set; } = isDebug;
}
