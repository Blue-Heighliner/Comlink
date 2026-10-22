namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnReceived"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog{TLevel, TProfile}"/>, which it asks for through dependency injection.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface INetworkReceivedContext<TFrame> where TFrame : class
{
    /// <summary>Gets the frame that was received. The engine never disposes it: if the frame type is <see cref="IDisposable"/>, disposing it is up to the handler.</summary>
    TFrame Frame { get; }

    /// <summary>Gets where the frame came from.</summary>
    FrameOrigin Origin { get; }

    /// <summary>
    /// Gets the name of the user the frame arrived from: the other end of the connection it came over, who is not necessarily who wrote it (a server hands on what its clients sent), or for a frame
    /// that arrived over the local interface or from an external system, the user running this node, and for an external system its name. The engine has authenticated it; the frame's own sender field has not been.
    /// </summary>
    string SourceUser { get; }
}
