namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnReceived"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkReceivedContext<TFrame, TPriority, TLevel, TAspect> : INetworkContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the frame that was received. The engine never disposes it: if the frame type is <see cref="IDisposable"/>, disposing it is up to the handler.</summary>
    TFrame Frame { get; }

    /// <summary>Gets where the frame came from.</summary>
    FrameOrigin Origin { get; }

    /// <summary>
    /// Gets the user the frame arrived from: the other end of the connection it came over, who is not necessarily who wrote it (a server hands on what its clients sent), or for a frame
    /// that arrived over the local interface or from an external system, the user running this node, and for an external system a user with only its name. The engine has authenticated it; the frame's own sender field has not been.
    /// </summary>
    UserInfo SourceUser { get; }
}
