namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkDisconnectedContext"/> a frame handler is told a user disconnected with.</summary>
/// <param name="targetUser">The user that disconnected.</param>
public sealed class FakeNetworkDisconnectedContext(UserInfo targetUser) : INetworkDisconnectedContext
{
    /// <inheritdoc />
    public UserInfo TargetUser { get; set; } = targetUser;
}
