namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnConnected"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog{TProfile}"/>, which it asks for through dependency injection.</summary>
public interface INetworkConnectedContext
{
    /// <summary>Gets the name of the user that connected. What is known about the user is in the <see cref="IUserCatalog{TProfile}"/>.</summary>
    string TargetUser { get; }
}
