namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkConnectedContext"/> a frame handler is told a user connected with.</summary>
/// <param name="targetUser">The user that connected.</param>
public sealed class FakeNetworkConnectedContext(UserInfo targetUser) : INetworkConnectedContext
{
    /// <inheritdoc />
    public UserInfo TargetUser { get; set; } = targetUser;
}
