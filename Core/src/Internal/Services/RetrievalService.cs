namespace BlueHeighliner.Comlink;

/// <summary>Asks the server a message is stored on for copies of the messages it stored that fit some criteria.</summary>
internal interface IRetrievalService
{
    /// <summary>
    /// Sends a retrieval request carrying <paramref name="criteria"/> to <paramref name="serverName"/> as an ordinary
    /// message of the configured frame type, from the current user. The server answers by sending copies of the
    /// matching stored messages back, which arrive as received messages; nothing is returned for them here.
    /// Returns whether the request reached the server's side of the network (not whether anything matched).
    /// </summary>
    /// <exception cref="InvalidOperationException">No user is installed yet.</exception>
    /// <exception cref="ArgumentException"><paramref name="serverName"/> is not one of the servers (see <see cref="IEngineController.StorageServers"/>), the only users a retrieval can be asked of.</exception>
    Task<bool> Request(string serverName, RetrievalCriteria criteria, CancellationToken cancellation = default);
}

/// <inheritdoc cref="IRetrievalService" />
internal sealed class RetrievalService : IRetrievalService
{
    /// <summary>Initializes a new <see cref="RetrievalService"/>.</summary>
    public RetrievalService(IEngineController engineController, ICurrentUserProvider currentUserProvider, IMessageRoutingService messageRouting)
    {
        this.engineController = engineController;
        this.currentUserProvider = currentUserProvider;
        this.messageRouting = messageRouting;
    }

    private readonly IEngineController engineController;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IMessageRoutingService messageRouting;

    /// <inheritdoc />
    public async Task<bool> Request(string serverName, RetrievalCriteria criteria, CancellationToken cancellation = default)
    {
        string user = currentUserProvider.UserName ?? throw new InvalidOperationException("A retrieval request needs an installed user.");
        if (!engineController.StorageServers.Contains(serverName, StringComparer.OrdinalIgnoreCase)) { throw new ArgumentException($"{serverName} is not a server, and a retrieval can only be asked of one", nameof(serverName)); }

        object request = engineController.CreateRetrieval(criteria, serverName);

        (_, IReadOnlyList<UserDeliveryResult> results) = await messageRouting.RouteFrame(user, request, cancellation);
        return results.Any(result => result.Success && string.Equals(result.UserName, serverName, StringComparison.OrdinalIgnoreCase));
    }
}
