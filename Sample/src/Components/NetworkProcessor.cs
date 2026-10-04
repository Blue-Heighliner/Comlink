namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Reacts to peer activity: any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply (via <see cref="INetworkContext{TFrame}.Send"/>). Servers and relays compose no messages of their own, only transport them, so they do nothing here;
/// connections and disconnections are not reacted to.
/// </summary>
public sealed class NetworkProcessor : INetworkProcessor<Frame>
{
    /// <inheritdoc />
    public void OnConnected(INetworkConnectedContext<Frame> context)
    {
    }

    /// <inheritdoc />
    public void OnDisconnected(INetworkDisconnectedContext<Frame> context)
    {
    }

    /// <inheritdoc />
    public void OnReceived(INetworkReceivedContext<Frame> context)
    {
        if (context.CurrentUser.Role is UserRole.Server or UserRole.Relay) { return; }

        Frame frame = context.Frame;
        if (frame.IsMessage && string.Equals(frame.Category, "PING", StringComparison.OrdinalIgnoreCase))
        {
            context.Send(new Frame { IsMessage = true, Text = "PONG", Recipients = [new Recipient { User = frame.Sender }] });
        }
    }
}
