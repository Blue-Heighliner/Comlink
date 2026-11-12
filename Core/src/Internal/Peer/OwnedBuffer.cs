namespace BlueHeighliner.Comlink;

/// <summary>Wraps a <see cref="PooledBufferWriter{T}"/> as an <see cref="IMemoryOwner{T}"/> so what was written can be handed out as a scoped, pool-backed buffer.</summary>
/// <typeparam name="T">The type of the items.</typeparam>
internal readonly struct OwnedBuffer<T> : IMemoryOwner<T>
{
    private readonly PooledBufferWriter<T>? writer;

    /// <summary>Initializes an <see cref="OwnedBuffer{T}"/> wrapping the given writer.</summary>
    internal OwnedBuffer(PooledBufferWriter<T> writer) => this.writer = writer;

    /// <inheritdoc />
    public Memory<T> Memory => writer?.WrittenMutableMemory ?? default;

    /// <inheritdoc />
    public void Dispose() => writer?.Release();
}
