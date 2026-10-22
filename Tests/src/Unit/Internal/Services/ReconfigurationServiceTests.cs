namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="ReconfigurationService"/>.</summary>
public sealed class ReconfigurationServiceTests
{
    private readonly Mock<IEngineController> controller = new();
    private readonly Mock<IUserService> users = new();
    private readonly Mock<IConfigurationHandler> configurationHandler = new();

    private ReconfigurationService Build() => new(controller.Object, users.Object, LoggerFactory.Create(_ => { }));

    /// <summary>The configuration handler is called every time it is run, while there is a current user.</summary>
    [Fact]
    public async Task Run_WithACurrentUser_CallsTheConfigurationHandlerEachTime()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInformation { Name = "ME" });
        controller.SetupGet(c => c.ConfigurationHandler).Returns(configurationHandler.Object);
        ReconfigurationService service = Build();

        await service.Run();
        await service.Run();

        configurationHandler.Verify(r => r.OnReconfigured(), Times.Exactly(2));
    }

    /// <summary>With no current user the configuration handler is never called, and is not even built.</summary>
    [Fact]
    public async Task Run_WithoutACurrentUser_NeverCallsTheConfigurationHandler()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns((UserInformation?)null);
        controller.SetupGet(c => c.ConfigurationHandler).Returns(configurationHandler.Object);

        await Build().Run();

        configurationHandler.Verify(r => r.OnReconfigured(), Times.Never);
        controller.VerifyGet(c => c.ConfigurationHandler, Times.Never);
    }

    /// <summary>Without a configuration handler nothing happens.</summary>
    [Fact]
    public async Task Run_WithoutAConfigurationHandler_DoesNothing()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInformation { Name = "ME" });
        controller.SetupGet(c => c.ConfigurationHandler).Returns((IConfigurationHandler?)null);

        await Build().Run();
    }

    /// <summary>A configuration handler that throws, or cannot be built, does not throw back into the engine.</summary>
    [Fact]
    public async Task Run_AConfigurationHandlerThatFails_IsLoggedNotThrown()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInformation { Name = "ME" });
        controller.SetupGet(c => c.ConfigurationHandler).Returns(configurationHandler.Object);
        configurationHandler.Setup(r => r.OnReconfigured()).ThrowsAsync(new InvalidOperationException("boom"));

        await Build().Run();

        controller.SetupGet(c => c.ConfigurationHandler).Throws(new InvalidOperationException("cannot build"));
        await Build().Run();
    }
}
