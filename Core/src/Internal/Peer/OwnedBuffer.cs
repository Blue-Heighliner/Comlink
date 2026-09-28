namespace BlueHeighliner.Comlink.Peer;

/// <summary>Wraps a <see cref="PooledArrayBufferWriter{T}"/> as an <see cref="IMemoryOwner{T}"/> so serialized bytes can be handed out as a scoped, pool-backed buffer.</summary>
internal readonly struct OwnedBuffer : IMemoryOwner<byte>
{
    private readonly PooledArrayBufferWriter<byte>? writer;

    /// <summary>Initializes an <see cref="OwnedBuffer"/> wrapping the given writer.</summary>
    internal OwnedBuffer(PooledArrayBufferWriter<byte> writer) => this.writer = writer;

    /// <inheritdoc />
    public Memory<byte> Memory => writer?.WrittenMutableMemory ?? default;

    /// <inheritdoc />
    public void Dispose() => writer?.Dispose();
}
