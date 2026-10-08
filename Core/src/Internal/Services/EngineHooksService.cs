namespace BlueHeighliner.Comlink;

/// <summary>Hands what <see cref="IPeerService"/> reports to the host's network processor: a user connecting, a user disconnecting and a frame arriving, in both Client and Headless mode.</summary>
internal interface IEngineHooksService
{
    /// <summary>Subscribes to <see cref="IPeerService"/>'s events and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IEngineHooksService" />
internal sealed class EngineHooksService(IPeerService peerService, IEngineController engineController, INetworkProcessing processing) : IEngineHooksService
{
    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (engineController.NetworkHandler is null)
        {
            return;
        }

        peerService.UserConnected += OnUserConnected;
        peerService.UserDisconnected += OnUserDisconnected;
        peerService.FrameReceived += OnFrameReceived;

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
        finally
        {
            peerService.UserConnected -= OnUserConnected;
            peerService.UserDisconnected -= OnUserDisconnected;
            peerService.FrameReceived -= OnFrameReceived;
        }
    }

    private Task OnUserConnected(string userName)
    {
        processing.Connected(userName);
        return Task.CompletedTask;
    }

    private Task OnUserDisconnected(string userName)
    {
        processing.Disconnected(userName);
        return Task.CompletedTask;
    }

    private Task OnFrameReceived(ReceivedFrame received)
    {
        processing.Received(received.Frame, FrameOrigin.Peer, received.SourceUser);
        return Task.CompletedTask;
    }
}
