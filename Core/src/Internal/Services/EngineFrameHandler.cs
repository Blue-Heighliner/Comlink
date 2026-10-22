namespace BlueHeighliner.Comlink;

/// <summary>The engine's view of a host's <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/>, with frames as plain objects since the engine does not know the host's type at compile time.</summary>
internal interface IEngineFrameHandler
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="userName">The user that connected.</param>
    Task OnConnected(INetworkEnvironment environment, string userName);

    /// <summary>Called when a user goes from having at least one live peer connection to having none.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="userName">The user that disconnected.</param>
    Task OnDisconnected(INetworkEnvironment environment, string userName);

    /// <summary>Called when a frame is received.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <param name="origin">Where it came from.</param>
    /// <param name="sourceUser">The user it arrived from, or the name of the external system.</param>
    Task OnReceived(INetworkEnvironment environment, object frame, FrameOrigin origin, string sourceUser);

    /// <summary>Called when the user sends a message with the GUI.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="message">The message.</param>
    Task OnSent(INetworkEnvironment environment, Message message);

    /// <summary>Called when the user opens a message they received.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="message">The message.</param>
    Task OnRead(INetworkEnvironment environment, Message message);

    /// <summary>Called when the user submits a retrieval with the GUI.</summary>
    /// <param name="environment">What the handler acts on.</param>
    /// <param name="server">The server asked.</param>
    /// <param name="criteria">What the stored messages must fit.</param>
    Task OnRetrieval(INetworkEnvironment environment, string server, RetrievalCriteria criteria);
}

/// <summary>Presents a host's <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> as an <see cref="IEngineFrameHandler"/>, giving it a context of the right kind for each event.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="handler">The host's handler.</param>
internal sealed class EngineFrameHandler<TFrame, TPriority, TLevel, TAspect>(IFrameHandler<TFrame, TPriority, TLevel, TAspect> handler) : IEngineFrameHandler where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public Task OnConnected(INetworkEnvironment environment, string userName) => handler.OnConnected(new NetworkConnectedContext(userName));

    /// <inheritdoc />
    public Task OnDisconnected(INetworkEnvironment environment, string userName) => handler.OnDisconnected(new NetworkDisconnectedContext(userName));

    /// <inheritdoc />
    public Task OnReceived(INetworkEnvironment environment, object frame, FrameOrigin origin, string sourceUser) => handler.OnReceived(new NetworkReceivedContext<TFrame>((TFrame)frame, origin, sourceUser));

    /// <inheritdoc />
    public async Task OnSent(INetworkEnvironment environment, Message message)
    {
        Message<TPriority, TLevel, TAspect> typed = message.ToTyped<TPriority, TLevel, TAspect>();
        IReadOnlySet<string> destinations = environment.GetDestinations(typed.MessageLevel is { } level ? (Enum)level : null, typed.Addresses.Where(address => address.Type is not AddressType.External).Select(address => address.UserName), out IReadOnlySet<string> excluded);
        foreach (string user in excluded)
        {
            await environment.SetSentStatus(message.Id, user, DestinationStatus.Failed);
        }

        if (destinations.Count > 0)
        {
            await handler.OnSent(new NetworkSentContext<TPriority, TLevel, TAspect>(typed, destinations));
        }
    }

    /// <inheritdoc />
    public Task OnRead(INetworkEnvironment environment, Message message) => handler.OnRead(new NetworkReadContext<TPriority, TLevel, TAspect>(message.ToTyped<TPriority, TLevel, TAspect>()));

    /// <inheritdoc />
    public Task OnRetrieval(INetworkEnvironment environment, string server, RetrievalCriteria criteria) => handler.OnRetrieval(new NetworkRetrievalContext(server, criteria));
}
