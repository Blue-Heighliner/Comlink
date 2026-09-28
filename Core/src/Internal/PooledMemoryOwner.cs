namespace BlueHeighliner.Comlink;

/// <summary>An <see cref="IMemoryOwner{T}"/> over a slice of an array rented from <see cref="ArrayPool{T}.Shared"/>, returned to the pool on the first <see cref="Dispose"/>.</summary>
internal sealed class PooledMemoryOwner : IMemoryOwner<byte>
{
    /// <summary>Rents an array of at least <paramref name="length"/> bytes and wraps it, exposing exactly <paramref name="length"/> bytes.</summary>
    internal static PooledMemoryOwner Rent(int length) => new(ArrayPool<byte>.Shared.Rent(length), length);

    /// <summary>Initializes an owner exposing the first <paramref name="length"/> bytes of <paramref name="array"/>, which must have been rented from the shared pool.</summary>
    internal PooledMemoryOwner(byte[] array, int length)
    {
        this.array = array;
        this.length = length;
    }

    private readonly int length;
    private byte[]? array;

    /// <inheritdoc />
    public Memory<byte> Memory => array is { } rented ? rented.AsMemory(0, length) : throw new ObjectDisposedException(nameof(PooledMemoryOwner));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref array, null) is { } rented) { ArrayPool<byte>.Shared.Return(rented); }
    }
}
