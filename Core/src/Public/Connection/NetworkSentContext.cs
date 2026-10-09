namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnSent"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkSentContext<TFrame, TPriority, TLevel, TAspect> : INetworkContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the message the user sent, as stored in the Outbox.</summary>
    Message<TPriority, TLevel, TAspect> Message { get; }

    /// <summary>
    /// Gets the users the message is for, built from its addresses: the user names of all its addresses that are not <see cref="AddressType.External"/>, with groups expanded. A user the message cannot be sent to, such as one whose
    /// message level ranks below the message's, is not included: the engine has already marked them <see cref="DestinationStatus.Failed"/>, so the handler only has to deal with these. There is always at least one: the handler is not called for a message with none.
    /// </summary>
    IReadOnlySet<string> Destinations { get; }
}
