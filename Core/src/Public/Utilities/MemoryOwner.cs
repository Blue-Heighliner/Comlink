namespace BlueHeighliner.Comlink;

/// <summary>An <see cref="IMemoryOwner{T}"/> over memory that something else owns: it hands out the memory it was given and disposing it does nothing.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="memory">The memory to hand out.</param>
public sealed class MemoryOwner<T>(Memory<T> memory) : IMemoryOwner<T>
{
    /// <inheritdoc />
    public Memory<T> Memory { get; } = memory;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
