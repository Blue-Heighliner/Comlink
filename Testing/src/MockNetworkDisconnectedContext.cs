namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkDisconnectedContext"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when a user disconnects.</summary>
public sealed class MockNetworkDisconnectedContext : Mock<INetworkDisconnectedContext>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="targetUser">The user that disconnected.</param>
    public MockNetworkDisconnectedContext(UserInfo targetUser)
    {
        TargetUser = targetUser;
        Setup(context => context.TargetUser).Returns(() => TargetUser);
    }

    /// <summary>Gets or sets what <see cref="INetworkDisconnectedContext.TargetUser"/> returns.</summary>
    public UserInfo TargetUser { get; set; }
}
