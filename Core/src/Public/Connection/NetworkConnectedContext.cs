namespace BlueHeighliner.Comlink;

/// <summary>Handed to <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnConnected"/>. What a handler acts with is in <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}"/> and <see cref="IUserCatalog"/>, which it asks for through dependency injection.</summary>
public interface INetworkConnectedContext
{
    /// <summary>Gets the user that connected.</summary>
    UserInfo TargetUser { get; }
}
