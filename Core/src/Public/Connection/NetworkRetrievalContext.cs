namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnRetrieval"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog{TLevel, TProfile}"/>, which it asks for through dependency injection.</summary>
public interface INetworkRetrievalContext
{
    /// <summary>Gets the name of the server the user asked, one of the users the network configuration states as servers. What is known about it is in the <see cref="IUserCatalog{TLevel, TProfile}"/>.</summary>
    string Server { get; }

    /// <summary>Gets what the stored messages must fit.</summary>
    RetrievalCriteria Criteria { get; }
}
