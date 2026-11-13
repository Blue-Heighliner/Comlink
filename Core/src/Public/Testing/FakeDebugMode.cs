namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDebugMode"/> service that says what it is told.</summary>
/// <param name="isEnabled">Whether the application is in debug mode.</param>
public sealed class FakeDebugMode(bool isEnabled = false) : IDebugMode
{
    /// <inheritdoc />
    public bool IsEnabled { get; set; } = isEnabled;
}
