namespace BlueHeighliner.Comlink.Engine;

/// <summary>An <see cref="IBufferWriter{T}"/> backed by a pooled array that grows by doubling.</summary>
internal sealed class PooledArrayBufferWriter<T> : IBufferWriter<T>, IDisposable
{
    private T[] _buffer;
    private int _written;
    private bool _disposed;

    /// <summary>Initializes a new writer with the specified initial capacity rented from the shared pool.</summary>
    internal PooledArrayBufferWriter(int initialCapacity = 256)
    {
        _buffer = ArrayPool<T>.Shared.Rent(initialCapacity);
    }

    /// <summary>Returns a <see cref="ReadOnlyMemory{T}"/> view over all bytes written so far.</summary>
    internal ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _written);
    /// <summary>Number of elements written to the buffer.</summary>
    internal int WrittenCount => _written;

    /// <inheritdoc />
    public void Advance(int count) => _written += count;

    /// <inheritdoc />
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    /// <inheritdoc />
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        int needed = _written + Math.Max(sizeHint, 1);
        if (needed <= _buffer.Length) return;

        int newSize = Math.Max(_buffer.Length * 2, needed);
        T[] newBuffer = ArrayPool<T>.Shared.Rent(newSize);
        _buffer.AsSpan(0, _written).CopyTo(newBuffer);
        ArrayPool<T>.Shared.Return(_buffer);
        _buffer = newBuffer;
    }

    /// <summary>Returns the underlying pooled array to the shared pool.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ArrayPool<T>.Shared.Return(_buffer);
    }
}
