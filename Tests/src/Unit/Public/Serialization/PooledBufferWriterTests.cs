namespace BlueHeighliner.Comlink.Tests.Unit.Public.Serialization;

/// <summary>Unit tests for <see cref="PooledBufferWriter{T}"/>.</summary>
public sealed class PooledBufferWriterTests
{
    /// <summary>What was written, even past the initial capacity, comes back as exactly those bytes.</summary>
    [Fact]
    public void ToOwner_HoldsExactlyWhatWasWritten()
    {
        PooledBufferWriter<byte> writer = new();
        byte[] bytes = [.. Enumerable.Range(0, 1000).Select(i => (byte)(i % 251))];

        writer.Write(bytes);
        using IMemoryOwner<byte> owner = writer.ToOwner();

        Assert.Equal(bytes, owner.Memory.ToArray());
    }

    /// <summary>Seeking back and writing overwrites in place without shortening what was written, and a position past the end or before the start is refused.</summary>
    [Fact]
    public void Position_SeekBackOverwritesAndKeepsTheLength()
    {
        PooledBufferWriter<byte> writer = new();
        writer.Write(new byte[] { 0, 0, 7, 8, 9 });
        Assert.Equal((5, 5), (writer.Length, writer.Position));

        writer.Position = 0;
        writer.Write(new byte[] { 1, 2 });
        Assert.Equal((5, 2), (writer.Length, writer.Position));
        writer.Position = writer.Length;
        writer.Write(new byte[] { 10 });

        using IMemoryOwner<byte> owner = writer.ToOwner();
        Assert.Equal(new byte[] { 1, 2, 7, 8, 9, 10 }, owner.Memory.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Position = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.Position = 7);
    }

    /// <summary>Any item type works, not only bytes: what was written comes back as exactly those items.</summary>
    [Fact]
    public void ToOwner_WorksForOtherItemTypes()
    {
        PooledBufferWriter<int> writer = new();

        writer.Write(new[] { 3, 1, 4, 1, 5 });
        using IMemoryOwner<int> owner = writer.ToOwner();

        Assert.Equal(new[] { 3, 1, 4, 1, 5 }, owner.Memory.ToArray());
    }

    /// <summary>Writing through GetMemory/Advance is reflected in WrittenMemory.</summary>
    [Fact]
    public void GetMemoryAndAdvance_WrittenMemoryReflectsWrittenBytes()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 4);

        Memory<byte> memory = writer.GetMemory(3);
        new byte[] { 1, 2, 3 }.CopyTo(memory);
        writer.Advance(3);

        Assert.Equal(new byte[] { 1, 2, 3 }, writer.WrittenMemory.ToArray());
    }

    /// <summary>GetSpan returns a span at the current write position and Advance moves past it, matching GetMemory's contract.</summary>
    [Fact]
    public void GetSpanAndAdvance_WrittenMemoryReflectsWrittenBytes()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 4);

        Span<byte> span = writer.GetSpan(3);
        new byte[] { 9, 8, 7 }.CopyTo(span);
        writer.Advance(3);

        Assert.Equal(new byte[] { 9, 8, 7 }, writer.WrittenMemory.ToArray());
    }

    /// <summary>Writing beyond the initial capacity grows the underlying buffer while preserving already-written bytes.</summary>
    [Fact]
    public void Advance_BeyondInitialCapacity_GrowsAndPreservesExistingBytes()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 2);

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

    /// <summary>WrittenMutableMemory reflects the same written bytes as WrittenMemory, as a mutable view.</summary>
    [Fact]
    public void WrittenMutableMemory_ReflectsWrittenBytes()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 4);

        Memory<byte> memory = writer.GetMemory(3);
        new byte[] { 4, 5, 6 }.CopyTo(memory);
        writer.Advance(3);

        Assert.Equal(new byte[] { 4, 5, 6 }, writer.WrittenMutableMemory.ToArray());
    }

    private sealed class RecordingPool : ArrayPool<byte>
    {
        public List<bool> Cleared { get; } = [];

        public override byte[] Rent(int minimumLength) => new byte[minimumLength];

        public override void Return(byte[] array, bool clearArray = false) => Cleared.Add(clearArray);
    }

    /// <summary>Every array goes back to the pool cleared, both the ones outgrown while writing and the last one on release, which a second release does not repeat.</summary>
    [Fact]
    public void Arrays_AreClearedWhenReturnedToThePool()
    {
        RecordingPool pool = new();
        PooledBufferWriter<byte> writer = new(initialCapacity: 2, pool);

        writer.GetSpan(100)[0] = 1;
        writer.Advance(1);
        writer.GetSpan(1000)[0] = 2;
        writer.Advance(1);
        writer.Release();
        writer.Release();

        Assert.Equal(3, pool.Cleared.Count);
        Assert.All(pool.Cleared, Assert.True);
    }

    /// <summary>Growing after seeking back keeps what lies beyond the position, not only what lies before it.</summary>
    [Fact]
    public void Growing_AfterSeekingBack_KeepsEverythingWritten()
    {
        PooledBufferWriter<byte> writer = new(initialCapacity: 4);
        writer.Write(new byte[] { 1, 2, 3, 4 });
        writer.Position = 1;

        writer.GetSpan(100)[0] = 9;
        writer.Advance(1);

        Assert.Equal(new byte[] { 1, 9, 3, 4 }, writer.WrittenMemory.ToArray());
    }
}
