namespace BlueHeighliner.Comlink;

/// <summary>Asks the host's network processor to ask a server for the messages it stored that fit some criteria.</summary>
internal interface IRetrievalService
{
    /// <summary>
    /// Hands <paramref name="criteria"/> to the host's network processor (see <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnRetrieval"/>), which sends the request to <paramref name="serverName"/> and whose answer arrives as received frames; nothing is returned for them here.
    /// Returns whether a processor took the request (not whether anything matched).
    /// </summary>
    /// <exception cref="InvalidOperationException">No user is installed yet.</exception>
    /// <exception cref="ArgumentException"><paramref name="serverName"/> is not one of the servers (see <see cref="IEngineController.StorageServers"/>), the only users a retrieval can be asked of.</exception>
    Task<bool> Request(string serverName, RetrievalCriteria criteria, CancellationToken cancellation = default);
}

/// <inheritdoc cref="IRetrievalService" />
internal sealed class RetrievalService(IEngineController engineController, ICurrentUserProvider currentUserProvider, INetworkProcessing processing) : IRetrievalService
{
    /// <inheritdoc />
    public Task<bool> Request(string serverName, RetrievalCriteria criteria, CancellationToken cancellation = default)
    {
        _ = currentUserProvider.UserName ?? throw new InvalidOperationException("A retrieval request needs an installed user.");
        if (!engineController.StorageServers.Contains(serverName, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{serverName} is not a server, and a retrieval can only be asked of one of the servers", nameof(serverName));
        }

        return Task.FromResult(processing.Retrieval(serverName, criteria));
    }
}
