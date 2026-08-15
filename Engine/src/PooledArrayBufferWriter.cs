namespace BlueHeighliner.Comlink.Engine;

/// <summary>An <see cref="IBufferWriter{T}"/> backed by a pooled array that grows by doubling.</summary>
internal sealed class PooledArrayBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    /// <summary>Initializes a new writer with the specified initial capacity rented from the shared pool.</summary>
    internal PooledArrayBufferWriter(int initialCapacity = 256)
    {
        buffer = ArrayPool<T>.Shared.Rent(initialCapacity);
    }

    private T[] buffer;
    private int written;
    private bool disposed;

    /// <summary>Returns a <see cref="ReadOnlyMemory{T}"/> view over all bytes written so far.</summary>
    internal ReadOnlyMemory<T> WrittenMemory => buffer.AsMemory(0, written);

    /// <summary>Number of elements written to the buffer.</summary>
    internal int WrittenCount => written;

    /// <inheritdoc />
    public void Advance(int count) => written += count;

    /// <inheritdoc />
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return buffer.AsMemory(written);
    }

    /// <inheritdoc />
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return buffer.AsSpan(written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        int needed = written + Math.Max(sizeHint, 1);
        if (needed <= buffer.Length) { return; }

        int newSize = Math.Max(buffer.Length * 2, needed);
        T[] newBuffer = ArrayPool<T>.Shared.Rent(newSize);
        buffer.AsSpan(0, written).CopyTo(newBuffer);
        ArrayPool<T>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    /// <summary>Returns the underlying pooled array to the shared pool.</summary>
    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        ArrayPool<T>.Shared.Return(buffer);
    }
}
