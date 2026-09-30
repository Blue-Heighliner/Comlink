namespace BlueHeighliner.Comlink.Tests;

/// <summary>A non-pooled <see cref="IMemoryOwner{T}"/> over a fixed array that records whether it was disposed, standing in for the pooled payloads MSMT and MicroGate hand to receivers.</summary>
internal sealed class TestOwner(byte[] data) : IMemoryOwner<byte>
{
    /// <inheritdoc />
    public Memory<byte> Memory { get; } = data;

    /// <summary>Gets a value indicating whether <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
