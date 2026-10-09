namespace BlueHeighliner.Comlink;

/// <summary>
/// One handshake a <see cref="HandshakePeerTransport"/> carries out on a new connection: a host's processor, told when the connection forms and given
/// each item that arrives until it marks the connection connected. Items are instances of the frame type or the packet type, serialized with that type's
/// serializer and sent as the next payload on the connection with nothing added to it, so an item is recognized by where it falls in the conversation, not by any marker.
/// </summary>
/// <param name="Serialize">Serializes an item with the matching serializer.</param>
/// <param name="Deserialize">Deserializes an item from the payload that carried it and, for a frame, the first packet that carried that payload.</param>
/// <param name="CarriesFrames">Whether the items are frames, which a packetizing transport beneath hands to the packet serializer, rather than packets.</param>
/// <param name="Processor">The host's processor.</param>
internal sealed record Handshake(Func<object, IMemoryOwner<byte>> Serialize, Func<ReadOnlyMemory<byte>, object?, object> Deserialize, bool CarriesFrames, IHandshakeHandler Processor)
{
    /// <summary>The packet handshake the engine is configured with, or <see langword="null"/> when there is none.</summary>
    /// <param name="engineController">The engine configuration to read the handshake from.</param>
    public static Handshake? ForPackets(IEngineController engineController)
    {
        if (engineController.PacketHandshakeProcessor is not { } processor)
        {
            return null;
        }

        IPacketSerializer serializer = engineController.PacketSerializer ?? throw new InvalidEngineConfigurationException("A packet handshake processor needs a packet serializer, but the engine controller has none");
        return new Handshake(packet => serializer.Serialize(packet, null), (data, _) => serializer.Deserialize(data), false, processor);
    }

    /// <summary>The frame handshake the engine is configured with, or <see langword="null"/> when there is none.</summary>
    /// <param name="engineController">The engine configuration to read the handshake from.</param>
    public static Handshake? ForFrames(IEngineController engineController)
        => engineController.FrameHandshakeProcessor is { } processor
            ? new Handshake(engineController.FrameSerializer.Serialize, engineController.FrameSerializer.Deserialize, true, processor)
            : null;
}
