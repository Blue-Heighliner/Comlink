namespace BlueHeighliner.Comlink;

/// <summary>
/// An <see cref="ISeekableBufferWriter{T}"/> over a pooled array that grows by doubling, for a serializer to write into so that the buffer it returns from
/// <see cref="IFrameSerializer.Serialize"/> or <see cref="IPacketSerializer.Serialize"/> comes from a pool instead of being allocated for every frame or packet.
/// Write the serialized bytes (a <c>Utf8JsonWriter</c>, for instance, takes one), then hand the result to the caller with <see cref="ToOwner"/>. Every array is cleared when it goes back to the pool, so what was written never lingers there.
/// </summary>
/// <typeparam name="T">The type of the items written, <see cref="byte"/> for serialized data.</typeparam>
public sealed class PooledBufferWriter<T> : ISeekableBufferWriter<T>
{
    /// <summary>Initializes an empty writer.</summary>
    public PooledBufferWriter()
        : this(256, null)
    {
    }

    /// <summary>Initializes an empty writer with the specified initial capacity rented from <paramref name="pool"/>, the shared pool unless one is given.</summary>
    internal PooledBufferWriter(int initialCapacity, ArrayPool<T>? pool = null)
    {
        this.pool = pool ?? ArrayPool<T>.Shared;
        buffer = this.pool.Rent(initialCapacity);
    }

    private readonly ArrayPool<T> pool;

    private T[] buffer;
    private int position;
    private int length;
    private bool released;

    /// <inheritdoc />
    public int Length => length;

    /// <inheritdoc />
    public int Position
    {
        get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, length);
            position = value;
        }
    }

    /// <summary>Gets a read-only view over everything written.</summary>
    internal ReadOnlyMemory<T> WrittenMemory => buffer.AsMemory(0, length);

    /// <summary>Gets a mutable view over everything written, for wrapping in an <see cref="IMemoryOwner{T}"/>.</summary>
    internal Memory<T> WrittenMutableMemory => buffer.AsMemory(0, length);

    /// <inheritdoc />
    public void Advance(int count)
    {
        position += count;
        length = Math.Max(length, position);
    }

    /// <inheritdoc />
    public Memory<T> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return buffer.AsMemory(position);
    }

    /// <inheritdoc />
    public Span<T> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return buffer.AsSpan(position);
    }

    /// <summary>Hands everything written over as a buffer whose <see cref="IMemoryOwner{T}.Memory"/> is exactly the written items and whose disposal clears the pooled array and returns it. The writer must not be used afterwards.</summary>
    public IMemoryOwner<T> ToOwner() => new OwnedBuffer<T>(this);

    /// <summary>Clears the underlying pooled array and returns it to the pool; calling it again does nothing.</summary>
    internal void Release()
    {
        if (released)
        {
            return;
        }

        released = true;
        pool.Return(buffer, clearArray: true);
    }

    private void EnsureCapacity(int sizeHint)
    {
        int needed = position + Math.Max(sizeHint, 1);
        if (needed <= buffer.Length)
        {
            return;
        }

        int newSize = Math.Max(buffer.Length * 2, needed);
        T[] newBuffer = pool.Rent(newSize);
        buffer.AsSpan(0, length).CopyTo(newBuffer);
        pool.Return(buffer, clearArray: true);
        buffer = newBuffer;
    }
}
