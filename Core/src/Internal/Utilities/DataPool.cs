namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="IDataPool" />
internal sealed class DataPool : IDataPool
{
    private readonly ArrayPool<byte> pool = ArrayPool<byte>.Create();

    /// <inheritdoc />
    public IMemoryOwner<byte> Rent(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return new Lease(pool, pool.Rent(size), size);
    }

    private sealed class Lease(ArrayPool<byte> pool, byte[] array, int length) : IMemoryOwner<byte>
    {
        private byte[]? rented = array;

        public Memory<byte> Memory => rented is { } current ? current.AsMemory(0, length) : throw new ObjectDisposedException(nameof(IMemoryOwner<byte>));

        public void Dispose()
        {
            if (Interlocked.Exchange(ref rented, null) is { } returned)
            {
                pool.Return(returned);
            }
        }
    }
}
