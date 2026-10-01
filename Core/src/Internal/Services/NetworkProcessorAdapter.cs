namespace BlueHeighliner.Comlink.Services;

/// <summary>Presents a host's <see cref="INetworkProcessor{TMessage}"/> as an <see cref="INetworkHandler"/>.</summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
/// <param name="processor">The host's processor.</param>
internal sealed class NetworkProcessorAdapter<TMessage>(INetworkProcessor<TMessage> processor) : INetworkHandler where TMessage : class
{
    /// <inheritdoc />
    public Task OnConnected(INetworkUserContext context) => processor.OnConnected(new TypedNetworkConnectedContext<TMessage>(context));

    /// <inheritdoc />
    public Task OnDisconnected(INetworkUserContext context) => processor.OnDisconnected(new TypedNetworkDisconnectedContext<TMessage>(context));

    /// <inheritdoc />
    public Task OnReceived(INetworkMessageContext context) => processor.OnReceived(new TypedNetworkReceivedContext<TMessage>(context));
}
