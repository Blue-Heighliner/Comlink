namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkRetrievalContext"/> a frame handler is handed when the user asks a server for stored messages.</summary>
/// <param name="server">The name of the server asked.</param>
/// <param name="criteria">What the user asked for.</param>
public sealed class FakeNetworkRetrievalContext(string server, RetrievalCriteria criteria) : INetworkRetrievalContext
{
    /// <inheritdoc />
    public string Server { get; set; } = server;

    /// <inheritdoc />
    public RetrievalCriteria Criteria { get; set; } = criteria;
}
