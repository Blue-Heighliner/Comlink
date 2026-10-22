namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDataPool"/> service that rents a new array of exactly the size asked for each time, so a test sees no reuse.</summary>
public sealed class FakeDataPool : IDataPool
{
    /// <inheritdoc />
    public IMemoryOwner<byte> Rent(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        return new MemoryOwner<byte>(new byte[size]);
    }
}
