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

    /// <summary>A returned buffer is wiped, so the next rental of that size never sees what the last user left, whether it was rented empty or as a copy.</summary>
    [Fact]
    public void Rent_AfterAReturn_IsCleared()
    {
        DataPool pool = new();
        IMemoryOwner<byte> first = pool.Rent(16);
        first.Memory.Span.Fill(0xAB);
        first.Dispose();
        IMemoryOwner<byte> copy = pool.RentCopy(Enumerable.Repeat((byte)0xCD, 16).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)0xCD, 16), copy.Memory.ToArray());
        copy.Dispose();

        using IMemoryOwner<byte> again = pool.Rent(16);

        Assert.All(again.Memory.ToArray(), value => Assert.Equal(0, value));
    }

    /// <summary>A copy holds exactly the bytes given, is independent of the source, and can be empty.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5000)]
    public void RentCopy_HoldsACopyOfTheData(int length)
    {
        byte[] source = [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

        using IMemoryOwner<byte> copy = new DataPool().RentCopy(source);
        if (length > 0)
        {
            source[0] ^= 0xFF;
        }

        Assert.Equal(length, copy.Memory.Length);
        Assert.Equal([.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))], copy.Memory.ToArray());
    }

    /// <summary>A copy is no longer usable once disposed.</summary>
    [Fact]
    public void RentCopy_Dispose_MakesItUnusable()
    {
        IMemoryOwner<byte> copy = new DataPool().RentCopy(new byte[] { 1, 2 });

        copy.Dispose();

        Assert.Throws<ObjectDisposedException>(() => copy.Memory);
    }
}
