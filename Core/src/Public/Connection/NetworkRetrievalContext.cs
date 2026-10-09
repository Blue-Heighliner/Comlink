namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnRetrieval"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkRetrievalContext<TFrame, TPriority, TLevel, TAspect> : INetworkContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the server the user asked, one of the users the network configuration states as servers.</summary>
    UserInfo Server { get; }

    /// <summary>Gets what the stored messages must fit.</summary>
    RetrievalCriteria Criteria { get; }
}
