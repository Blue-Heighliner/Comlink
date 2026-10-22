namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnDisconnected"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog{TProfile}"/>, which it asks for through dependency injection.</summary>
public interface INetworkDisconnectedContext
{
    /// <summary>Gets the name of the user that disconnected. What is known about the user is in the <see cref="IUserCatalog{TProfile}"/>.</summary>
    string TargetUser { get; }
}
