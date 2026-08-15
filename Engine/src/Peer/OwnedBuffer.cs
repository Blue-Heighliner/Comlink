namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>Wraps a <see cref="PooledArrayBufferWriter{T}"/> to provide a scoped, disposable view of serialized bytes.</summary>
internal readonly struct OwnedBuffer : IDisposable
{
    private readonly PooledArrayBufferWriter<byte>? writer;

    /// <summary>Initializes an <see cref="OwnedBuffer"/> wrapping the given writer.</summary>
    internal OwnedBuffer(PooledArrayBufferWriter<byte> writer) => this.writer = writer;

    /// <summary>Returns the span of bytes written to the underlying writer.</summary>
    internal ReadOnlyMemory<byte> Memory => writer?.WrittenMemory ?? default;

    /// <inheritdoc />
    public void Dispose() => writer?.Dispose();
}
