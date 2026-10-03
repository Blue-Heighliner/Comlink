namespace BlueHeighliner.Comlink;

/// <summary>Presents a host's <see cref="INetworkProcessor{TFrame}"/> as an <see cref="INetworkHandler"/>.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <param name="processor">The host's processor.</param>
internal sealed class NetworkProcessorAdapter<TFrame>(INetworkProcessor<TFrame> processor) : INetworkHandler where TFrame : class
{
    /// <inheritdoc />
    public void OnConnected(INetworkUserContext context) => processor.OnConnected(new TypedNetworkConnectedContext<TFrame>(context));

    /// <inheritdoc />
    public void OnDisconnected(INetworkUserContext context) => processor.OnDisconnected(new TypedNetworkDisconnectedContext<TFrame>(context));

    /// <inheritdoc />
    public void OnReceived(INetworkFrameContext context) => processor.OnReceived(new TypedNetworkReceivedContext<TFrame>(context));
}
