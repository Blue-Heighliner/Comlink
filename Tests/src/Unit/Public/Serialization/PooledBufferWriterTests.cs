namespace BlueHeighliner.Comlink.Tests.Unit.Public.Serialization;

/// <summary>Unit tests for <see cref="PooledBufferWriter"/>.</summary>
public sealed class PooledBufferWriterTests
{
    /// <summary>What was written, even past the initial capacity, comes back as exactly those bytes.</summary>
    [Fact]
    public void ToOwner_HoldsExactlyWhatWasWritten()
    {
        PooledBufferWriter writer = new();
        byte[] bytes = [.. Enumerable.Range(0, 1000).Select(i => (byte)(i % 251))];

        writer.Write(bytes);
        using IMemoryOwner<byte> owner = writer.ToOwner();

        Assert.Equal(bytes, owner.Memory.ToArray());
    }
}
