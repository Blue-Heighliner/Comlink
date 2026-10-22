namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkDisconnectedContext"/> a frame handler is told a user disconnected with.</summary>
/// <param name="targetUser">The name of the user that disconnected.</param>
public sealed class FakeNetworkDisconnectedContext(string targetUser) : INetworkDisconnectedContext
{
    /// <inheritdoc />
    public string TargetUser { get; set; } = targetUser;
}
