namespace BlueHeighliner.Comlink;

/// <summary>
/// The handshake a <see cref="HandshakePeerTransport"/> carries out on a new connection: a host's handshake processor, told when the connection forms and given
/// each packet that arrives until it marks the connection connected. Packets are serialized with the packet serializer and sent as the next payload on the connection
/// with nothing added to them, so a packet is recognized by where it falls in the conversation, not by any marker.
/// </summary>
/// <param name="Serialize">Serializes a packet with the packet serializer.</param>
/// <param name="Deserialize">Deserializes a packet from the payload that carried it.</param>
/// <param name="Processor">The host's processor.</param>
internal sealed record Handshake(Func<object, IMemoryOwner<byte>> Serialize, Func<ReadOnlyMemory<byte>, object> Deserialize, IHandshakeHandler Processor)
{
    /// <summary>The handshake the engine is configured with, or <see langword="null"/> when there is none.</summary>
    /// <param name="engineController">The engine configuration to read the handshake from.</param>
    public static Handshake? ForProcessor(IEngineController engineController)
    {
        if (engineController.HandshakeProcessor is not { } processor)
        {
            return null;
        }

        IPacketSerializer serializer = engineController.PacketSerializer ?? throw new InvalidEngineConfigurationException("A handshake processor needs a packet serializer, but the engine controller has none");
        return new Handshake(packet => serializer.Serialize(packet, null), serializer.Deserialize, processor);
    }
}
