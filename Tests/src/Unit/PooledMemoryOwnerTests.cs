namespace BlueHeighliner.Comlink.Tests.Unit;

/// <summary>Unit tests for <see cref="PooledMemoryOwner"/>.</summary>
public sealed class PooledMemoryOwnerTests
{
    /// <summary>The owner exposes exactly the requested length, even though the pool may rent a larger array.</summary>
    [Fact]
    public void Rent_ExposesExactlyTheRequestedLength()
    {
        using PooledMemoryOwner owner = PooledMemoryOwner.Rent(10);

        Assert.Equal(10, owner.Memory.Length);
    }

    /// <summary>Memory can be written and read back through the owner.</summary>
    [Fact]
    public void Memory_IsWritable()
    {
        using PooledMemoryOwner owner = PooledMemoryOwner.Rent(3);

        new byte[] { 1, 2, 3 }.CopyTo(owner.Memory);

        Assert.Equal(new byte[] { 1, 2, 3 }, owner.Memory.ToArray());
    }

    /// <summary>Disposing twice is a safe no-op the second time, so the array is never returned to the pool twice.</summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        PooledMemoryOwner owner = PooledMemoryOwner.Rent(4);

        owner.Dispose();
        owner.Dispose();
    }

    /// <summary>Reading the memory after disposal throws rather than exposing an array the pool may have handed to someone else.</summary>
    [Fact]
    public void Memory_AfterDispose_Throws()
    {
        PooledMemoryOwner owner = PooledMemoryOwner.Rent(4);
        owner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => owner.Memory);
    }
}
