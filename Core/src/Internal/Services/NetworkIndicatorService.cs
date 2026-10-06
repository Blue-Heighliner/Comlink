namespace BlueHeighliner.Comlink;

/// <summary>
/// Keeps the network indicator of a client or relay (see <see cref="INetworkIndicator"/>) showing whether the node is connected to its parent, directly: a client whose parent is a relay shows online while its connection to that relay is up,
/// whatever lies beyond it. A host's network processor can take this over by returning <see langword="false"/> from <see cref="INetworkProcessor{TFrame}.UseAutomaticNetworkIndicator"/>, after which only it sets the indicator.
/// A server has no indicator, so nothing runs for one.
/// </summary>
internal interface INetworkIndicatorService
{
    /// <summary>Starts following the connection to the parent, unless this node is a server or the network processor has taken the indicator over, and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="INetworkIndicatorService" />
internal sealed class NetworkIndicatorService(IPeerService peerService, IEngineController engineController, IEngineContextFactory contexts, INetworkIndicator indicator) : INetworkIndicatorService
{
    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (engineController.Role == UserRole.Server) { return; }
        if (engineController.NetworkHandler is { } handler && !handler.UseAutomaticNetworkIndicator(contexts.Create())) { return; }

        peerService.UserConnected += OnUserConnected;
        peerService.UserDisconnected += OnUserDisconnected;
        indicator.Set(engineController.ParentUser is { } parent && peerService.IsUserConnected(parent));

        try { await Task.Delay(Timeout.Infinite, cancellation); }
        catch (OperationCanceledException) { }
        finally
        {
            peerService.UserConnected -= OnUserConnected;
            peerService.UserDisconnected -= OnUserDisconnected;
        }
    }

    private Task OnUserConnected(string userName)
    {
        if (IsParent(userName)) { indicator.Set(true); }
        return Task.CompletedTask;
    }

    private Task OnUserDisconnected(string userName)
    {
        if (IsParent(userName)) { indicator.Set(false); }
        return Task.CompletedTask;
    }

    private bool IsParent(string userName) => string.Equals(engineController.ParentUser, userName, StringComparison.OrdinalIgnoreCase);
}
