namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDataPool"/> service that rents a new, zeroed array of exactly the size asked for (or a copy of the data given) each time, so a test sees no reuse.</summary>
public sealed class FakeDataPool : IDataPool
{
    /// <inheritdoc />
    public IMemoryOwner<byte> Rent(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return new MemoryOwner<byte>(new byte[size]);
    }

    /// <inheritdoc />
    public IMemoryOwner<byte> RentCopy(ReadOnlySpan<byte> data) => new MemoryOwner<byte>(data.ToArray());
}
