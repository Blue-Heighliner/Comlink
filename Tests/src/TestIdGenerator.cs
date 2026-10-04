namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IIdGenerator"/> that returns the default GUID identifiers without keeping anything.</summary>
internal sealed class TestIdGenerator : IIdGenerator
{
    /// <summary>Gets the shared instance.</summary>
    public static TestIdGenerator Instance { get; } = new();

    /// <inheritdoc />
    public Task<string> Next() => Task.FromResult(Guid.NewGuid().ToString("N").ToUpperInvariant());
}
