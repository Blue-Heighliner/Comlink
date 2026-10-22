namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkRetrievalContext" />
/// <param name="Server">The server the user asked.</param>
/// <param name="Criteria">What the stored messages must fit.</param>
internal sealed record NetworkRetrievalContext(string Server, RetrievalCriteria Criteria) : INetworkRetrievalContext;
