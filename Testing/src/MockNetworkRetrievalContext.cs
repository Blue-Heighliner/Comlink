namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkRetrievalContext"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when the user submits a retrieval.</summary>
public sealed class MockNetworkRetrievalContext : Mock<INetworkRetrievalContext>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="server">The server the user asked.</param>
    /// <param name="criteria">What the stored messages must fit.</param>
    public MockNetworkRetrievalContext(UserInfo server, RetrievalCriteria criteria)
    {
        Server = server;
        Criteria = criteria;
        Setup(context => context.Server).Returns(() => Server);
        Setup(context => context.Criteria).Returns(() => Criteria);
    }

    /// <summary>Gets or sets what <see cref="INetworkRetrievalContext.Server"/> returns.</summary>
    public UserInfo Server { get; set; }

    /// <summary>Gets or sets what <see cref="INetworkRetrievalContext.Criteria"/> returns.</summary>
    public RetrievalCriteria Criteria { get; set; }
}
