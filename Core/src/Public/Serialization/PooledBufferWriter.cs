namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// An <see cref="IBufferWriter{T}"/> over a pooled array, for a serializer to write into so that the buffer it returns from
/// <see cref="IFrameSerializer.Serialize"/> or <see cref="IPacketSerializer.Serialize"/> comes from a pool instead of being allocated for every frame or packet.
/// Write the serialized bytes (a <c>Utf8JsonWriter</c>, for instance, takes one), then hand the result to the caller with <see cref="ToOwner"/>.
/// </summary>
public sealed class PooledBufferWriter : IBufferWriter<byte>
{
    private readonly PooledArrayBufferWriter<byte> writer = new();

    /// <inheritdoc />
    public void Advance(int count) => writer.Advance(count);

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0) => writer.GetMemory(sizeHint);

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0) => writer.GetSpan(sizeHint);

    /// <summary>Hands everything written over as a buffer whose <see cref="IMemoryOwner{T}.Memory"/> is exactly the written bytes and whose disposal returns the pooled array. The writer must not be used afterwards.</summary>
    public IMemoryOwner<byte> ToOwner() => new OwnedBuffer(writer);
}
