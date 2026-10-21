namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetworkConnectedContext" />
/// <param name="TargetUser">The user that connected.</param>
internal sealed record NetworkConnectedContext(UserInfo TargetUser) : INetworkConnectedContext;
