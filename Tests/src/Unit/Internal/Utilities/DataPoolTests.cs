namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Utilities;

/// <summary>Unit tests for <see cref="DataPool"/>.</summary>
public sealed class DataPoolTests
{
    /// <summary>A rented buffer is exactly the size asked for, even when the pool's array is larger.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(5000)]
    public void Rent_GivesExactlyTheSizeAsked(int size)
    {
        using IMemoryOwner<byte> owner = new DataPool().Rent(size);

        Assert.Equal(size, owner.Memory.Length);
    }

    /// <summary>A negative size is refused.</summary>
    [Fact]
    public void Rent_Negative_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => new DataPool().Rent(-1));

    /// <summary>The buffer can be written, and once disposed it is no longer usable, however often it is disposed.</summary>
    [Fact]
    public void Dispose_ReturnsTheBuffer_AndMakesItUnusable()
    {
        IMemoryOwner<byte> owner = new DataPool().Rent(4);
        owner.Memory.Span.Fill(7);
        Assert.Equal([7, 7, 7, 7], owner.Memory.ToArray());

        owner.Dispose();
        owner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => owner.Memory);
    }

    /// <summary>Buffers rented together are separate, and a returned one is reused by the next rental of that size.</summary>
    [Fact]
    public void Rent_GivesSeparateBuffers_AndReusesReturnedOnes()
    {
        DataPool pool = new();
        IMemoryOwner<byte> first = pool.Rent(16);
        IMemoryOwner<byte> second = pool.Rent(16);
        first.Memory.Span[0] = 1;
        second.Memory.Span[0] = 2;
        Assert.Equal((1, 2), (first.Memory.Span[0], second.Memory.Span[0]));

        first.Memory.Span[1] = 42;
        first.Dispose();
        using IMemoryOwner<byte> third = pool.Rent(16);

        Assert.Equal(16, third.Memory.Length);
        second.Dispose();
    }
}
