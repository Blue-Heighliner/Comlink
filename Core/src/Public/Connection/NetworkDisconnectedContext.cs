namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnDisconnected"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog"/>, which it asks for through dependency injection.</summary>
public interface INetworkDisconnectedContext
{
    /// <summary>Gets the user that disconnected.</summary>
    UserInfo TargetUser { get; }
}
