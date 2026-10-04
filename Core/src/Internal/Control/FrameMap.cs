namespace BlueHeighliner.Comlink;

/// <summary>The engine-side form of a frame mapping: every accessor takes the frame as an <see cref="object"/>, since the engine works with the host's frame type only through <see cref="IEngineController"/>.</summary>
internal sealed class FrameMap
{
    /// <summary>The host's frame type.</summary>
    public required Type Type { get; init; }
    /// <summary>The serializer for the frame type.</summary>
    public required ServiceRegistration<IFrameSerializer> Serializer { get; init; }
    /// <summary>Creates a new, empty frame.</summary>
    public required Func<object> Create { get; init; }
    /// <summary>Gets how the host's message handler is instantiated.</summary>
    public required ServiceRegistration<IMessageFrameHandler> Message { get; init; }
    /// <summary>Gets how the host's retrieval request handler is instantiated.</summary>
    public required ServiceRegistration<IRetrievalFrameHandler> Retrieval { get; init; }
    /// <summary>Gets how the host's read receipt handler is instantiated.</summary>
    public required ServiceRegistration<IReceiptFrameHandler> ReadReceipt { get; init; }
    /// <summary>Gets how the host's receive receipt handler is instantiated.</summary>
    public required ServiceRegistration<IReceiptFrameHandler> ReceiveReceipt { get; init; }
    /// <summary>Gets the heartbeat handler, or <see langword="null"/> when none is stated, in which case no heartbeats are sent.</summary>
    public ServiceRegistration<IHeartbeatFrameHandler>? Heartbeat { get; init; }
}
