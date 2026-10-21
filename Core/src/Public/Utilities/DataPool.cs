namespace BlueHeighliner.Comlink;

/// <summary>Rents buffers from a pool of its own, so bytes that are needed only briefly (a serialized frame, a packet being built) are not allocated afresh each time. Ask for it through dependency injection.</summary>
public interface IDataPool
{
    /// <summary>
    /// Rents a buffer of <paramref name="size"/> bytes. Its <see cref="IMemoryOwner{T}.Memory"/> is exactly <paramref name="size"/> long and may hold what an earlier user left in it, so it is not cleared; disposing the owner returns the
    /// buffer to the pool, after which neither the owner nor its memory may be used.
    /// </summary>
    /// <param name="size">The number of bytes needed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    IMemoryOwner<byte> Rent(int size);
}
