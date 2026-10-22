namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkDisconnectedContext" />
/// <param name="TargetUser">The user that disconnected.</param>
internal sealed record NetworkDisconnectedContext(string TargetUser) : INetworkDisconnectedContext;
