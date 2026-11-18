namespace BlueHeighliner.Comlink;

/// <summary>Rents buffers from a pool of its own, so bytes that are needed only briefly (a serialized frame, a packet being built) are not allocated afresh each time. Ask for it through dependency injection.</summary>
public interface IDataPool
{
    /// <summary>
    /// Rents a buffer of <paramref name="size"/> bytes. Its <see cref="IMemoryOwner{T}.Memory"/> is exactly <paramref name="size"/> long and is cleared: every buffer is wiped when it goes back to the pool, so none holds what an earlier user
    /// left in it. Disposing the owner returns the buffer to the pool, after which neither the owner nor its memory may be used.
    /// </summary>
    /// <param name="size">The number of bytes needed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    IMemoryOwner<byte> Rent(int size);

    /// <summary>
    /// Rents a buffer holding a copy of <paramref name="data"/>, for bytes that must outlive the span they came from or be handed on as an owner. Its <see cref="IMemoryOwner{T}.Memory"/> is exactly as long as <paramref name="data"/> and, like
    /// any buffer of the pool, is wiped when it goes back; disposing the owner returns it, after which neither the owner nor its memory may be used.
    /// </summary>
    /// <param name="data">The bytes to copy.</param>
    IMemoryOwner<byte> RentCopy(ReadOnlySpan<byte> data);
}
