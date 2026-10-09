namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkRetrievalContext{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal sealed class NetworkRetrievalContext<TFrame, TPriority, TLevel, TAspect> : NetworkEngineContext<TFrame, TPriority, TLevel, TAspect>, INetworkRetrievalContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Creates the context for a retrieval the user submitted.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="server">The name of the server asked.</param>
    /// <param name="criteria">What the stored messages must fit.</param>
    public NetworkRetrievalContext(INetworkEnvironment environment, string server, RetrievalCriteria criteria)
        : base(environment)
    {
        Server = GetUser(server);
        Criteria = criteria;
    }

    /// <inheritdoc />
    public UserInfo Server { get; }

    /// <inheritdoc />
    public RetrievalCriteria Criteria { get; }
}
