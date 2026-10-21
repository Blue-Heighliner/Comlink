namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="INetworkConnectedContext"/> an <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> is handed when a user connects.</summary>
public sealed class MockNetworkConnectedContext : Mock<INetworkConnectedContext>
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="targetUser">The user that connected.</param>
    public MockNetworkConnectedContext(UserInfo targetUser)
    {
        TargetUser = targetUser;
        Setup(context => context.TargetUser).Returns(() => TargetUser);
    }

    /// <summary>Gets or sets what <see cref="INetworkConnectedContext.TargetUser"/> returns.</summary>
    public UserInfo TargetUser { get; set; }
}
