namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="INetworkConnectedContext"/> a frame handler is told a user connected with.</summary>
/// <param name="targetUser">The name of the user that connected.</param>
public sealed class FakeNetworkConnectedContext(string targetUser) : INetworkConnectedContext
{
    /// <inheritdoc />
    public string TargetUser { get; set; } = targetUser;
}
