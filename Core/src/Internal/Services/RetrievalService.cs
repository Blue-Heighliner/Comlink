namespace BlueHeighliner.Comlink.Services;

/// <summary>Asks a storage server (see <see cref="UserInfo.StoresMessages"/>) for copies of the messages it stored that fit some criteria.</summary>
internal interface IRetrievalService
{
    /// <summary>
    /// Sends a retrieval request carrying <paramref name="criteria"/> to <paramref name="serverName"/> as an ordinary
    /// message of the configured message type, from the current user. The server answers by sending copies of the
    /// matching stored messages back, which arrive as received messages; nothing is returned for them here.
    /// Returns whether the request reached the server's side of the network (not whether anything matched).
    /// </summary>
    /// <exception cref="InvalidOperationException">No user is installed yet.</exception>
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

        object request = engineController.CreateMessage();
        engineController.SetRetrieval(request, criteria);
        engineController.SetAddresses(request, [new MessageAddress { UserName = serverName, Type = AddressType.To }]);

        (_, IReadOnlyList<UserDeliveryResult> results) = await messageRouting.RouteMessage(user, request, cancellation);
        return results.Any(result => result.Success && string.Equals(result.UserName, serverName, StringComparison.OrdinalIgnoreCase));
    }
}
