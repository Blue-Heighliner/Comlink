namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="NetworkEnvironment"/>.</summary>
public sealed class NetworkEnvironmentTests
{
    private static NetworkEnvironment Build(Mock<TestEngineController> controller)
        => new(Mock.Of<IServiceProvider>(), controller.Object, Mock.Of<IEngineContextFactory>(), Mock.Of<IEntryService>(), Mock.Of<IMessageEvents>(), Mock.Of<IMessageStorageService>(), Mock.Of<IAutoForwardTargetsRepository>(), Mock.Of<INetworkIndicator>(), LoggerFactory.Create(_ => { }));

    /// <summary>A user is at least a level when their own level ranks at or above it, and a user with no level of their own runs at the lowest.</summary>
    [Fact]
    public void IsAtLeast_ComparesTheUsersLevelWithTheGivenOne()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.GetUserMessageLevel("ALICE")).Returns("SECRET");
        controller.Setup(c => c.GetUserMessageLevel("BOB")).Returns("INTERNAL");
        NetworkEnvironment environment = Build(controller);

        Assert.True(environment.IsAtLeast("ALICE", TestLevel.Secret));
        Assert.True(environment.IsAtLeast("BOB", TestLevel.Internal));
        Assert.False(environment.IsAtLeast("BOB", TestLevel.Secret));
        Assert.False(environment.IsAtLeast("NOBODY", TestLevel.Internal));
        Assert.True(environment.IsAtLeast("NOBODY", TestLevel.Public));
    }
}
