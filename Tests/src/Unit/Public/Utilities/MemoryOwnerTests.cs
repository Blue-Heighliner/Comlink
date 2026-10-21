namespace BlueHeighliner.Comlink.Tests.Unit.Public.Utilities;

/// <summary>Unit tests for <see cref="MemoryOwner{T}"/>.</summary>
public sealed class MemoryOwnerTests
{
    /// <summary>The owner hands out exactly the memory it was given, and disposing it leaves that memory usable.</summary>
    [Fact]
    public void Memory_IsWhatWasGiven_AndDisposeDoesNothing()
    {
        byte[] data = [1, 2, 3];
        MemoryOwner<byte> owner = new(data.AsMemory(1));

        Assert.Equal([2, 3], owner.Memory.ToArray());

        owner.Dispose();
        owner.Dispose();
        owner.Memory.Span[0] = 9;

        Assert.Equal([1, 9, 3], data);
    }
}
