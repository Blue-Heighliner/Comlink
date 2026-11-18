namespace BlueHeighliner.Comlink.Tests.Unit.Internal;

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

    /// <summary>What the owner held is wiped when it goes back to the shared pool, so received content does not linger there.</summary>
    [Fact]
    public void Dispose_WipesTheArray()
    {
        byte[] array = ArrayPool<byte>.Shared.Rent(64);
        PooledMemoryOwner owner = new(array, 8);
        owner.Memory.Span.Fill(0xAB);
        Assert.Equal(0xAB, array[0]);

        owner.Dispose();

        Assert.All(array.Take(8), value => Assert.Equal(0, value));
    }
}
