namespace BlueHeighliner.Comlink.Tests.Unit;

/// <summary>Unit tests for <see cref="PooledArrayBufferWriter{T}"/>.</summary>
public sealed class PooledArrayBufferWriterTests
{
    /// <summary>Writing through GetMemory/Advance is reflected in WrittenMemory.</summary>
    [Fact]
    public void GetMemoryAndAdvance_WrittenMemoryReflectsWrittenBytes()
    {
        using PooledArrayBufferWriter<byte> writer = new(initialCapacity: 4);

        Memory<byte> memory = writer.GetMemory(3);
        new byte[] { 1, 2, 3 }.CopyTo(memory);
        writer.Advance(3);

        Assert.Equal(new byte[] { 1, 2, 3 }, writer.WrittenMemory.ToArray());
    }

    /// <summary>GetSpan returns a span at the current write position and Advance moves past it, matching GetMemory's contract.</summary>
    [Fact]
    public void GetSpanAndAdvance_WrittenMemoryReflectsWrittenBytes()
    {
        using PooledArrayBufferWriter<byte> writer = new(initialCapacity: 4);

        Span<byte> span = writer.GetSpan(3);
        new byte[] { 9, 8, 7 }.CopyTo(span);
        writer.Advance(3);

        Assert.Equal(new byte[] { 9, 8, 7 }, writer.WrittenMemory.ToArray());
    }

    /// <summary>Writing beyond the initial capacity grows the underlying buffer while preserving already-written bytes.</summary>
    [Fact]
    public void Advance_BeyondInitialCapacity_GrowsAndPreservesExistingBytes()
    {
        using PooledArrayBufferWriter<byte> writer = new(initialCapacity: 2);

        writer.GetSpan(2)[..2].Fill(0xAA);
        writer.Advance(2);

        Span<byte> grown = writer.GetSpan(10);
        Assert.True(grown.Length >= 10);
        grown[..10].Fill(0xBB);
        writer.Advance(10);

        byte[] result = writer.WrittenMemory.ToArray();
        Assert.Equal(12, result.Length);
        Assert.All(result[..2], b => Assert.Equal(0xAA, b));
        Assert.All(result[2..], b => Assert.Equal(0xBB, b));
    }

    /// <summary>Disposing more than once is a safe no-op the second time.</summary>
    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        PooledArrayBufferWriter<byte> writer = new();
        writer.Dispose();
        writer.Dispose();
    }
}
