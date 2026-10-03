namespace BlueHeighliner.Comlink;

/// <summary>A payload an <see cref="IPacketAssembler"/> has put back together, with the first packet that carried it.</summary>
/// <param name="Payload">The complete payload, which the owner must dispose.</param>
/// <param name="FirstPacket">The packet at index zero of the payload, an instance of the host's packet type.</param>
internal sealed record AssembledPayload(IMemoryOwner<byte> Payload, object FirstPacket);
