namespace BlueHeighliner.Comlink;

/// <summary>One packet a payload was broken into by an <see cref="IPacketizer"/>: the serialized bytes to put on the wire, and the priority to send them at.</summary>
internal sealed record Packet : IDisposable
{
    /// <summary>Gets the packet's serialized bytes. Pool-backed, so it is only valid until the packet is disposed.</summary>
    public required IMemoryOwner<byte> Data { get; init; }

    /// <summary>Gets the priority to send this packet at; a packet with a higher priority is transmitted before queued packets with a lower one, whichever payload they belong to.</summary>
    public required int Priority { get; init; }

    /// <summary>Returns <see cref="Data"/> to its pool. Call once the packet has been sent.</summary>
    public void Dispose() => Data.Dispose();
}
