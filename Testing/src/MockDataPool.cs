namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IDataPool"/> the engine offers. Each rental is a new array of exactly the size asked for, so nothing is shared between rentals, and disposing it does nothing.</summary>
public sealed class MockDataPool : Mock<IDataPool>
{
    /// <summary>Initializes the mock.</summary>
    public MockDataPool() => Setup(pool => pool.Rent(It.IsAny<int>())).Returns((int size) => new MemoryOwner<byte>(new byte[size]));
}
