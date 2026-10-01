namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>
/// One initial exchange a <see cref="HandshakePeerTransport"/> carries out on a new connection: a host's processor, told when the connection forms and given
/// each item that arrives until it marks the connection connected. Items are instances of the message type or the packet type, serialized with that type's
/// serializer and sent as the next payload on the connection with nothing added to it, so an item is recognized by where it falls in the conversation, not by any marker.
/// </summary>
/// <param name="Serializer">Serializes the items.</param>
/// <param name="Processor">The host's processor.</param>
internal sealed record Handshake(INetworkSerializer Serializer, IInitialProcessor Processor)
{
    /// <summary>The initial packet exchange the engine is configured with, or <see langword="null"/> when there is none.</summary>
    /// <param name="engineController">The engine configuration to read the exchange from.</param>
    public static Handshake? ForPackets(IEngineController engineController)
        => engineController.InitialPacketProcessor is { } processor
            ? new Handshake(engineController.PacketSerializer ?? throw new InvalidOperationException("An initial packet processor needs a packet serializer, but the engine controller has none"), processor)
            : null;

    /// <summary>The initial message exchange the engine is configured with, or <see langword="null"/> when there is none.</summary>
    /// <param name="engineController">The engine configuration to read the exchange from.</param>
    public static Handshake? ForMessages(IEngineController engineController)
        => engineController.InitialMessageProcessor is { } processor ? new Handshake(engineController.NetworkSerializer, processor) : null;
}
