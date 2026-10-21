namespace BlueHeighliner.Comlink.Tests.Unit.Internal.Services;

/// <summary>Unit tests for <see cref="ReconfigurationService"/>.</summary>
public sealed class ReconfigurationServiceTests
{
    private readonly Mock<IEngineController> controller = new();
    private readonly Mock<IUserService> users = new();
    private readonly Mock<IReconfigurer> reconfigurer = new();

    private ReconfigurationService Build() => new(controller.Object, users.Object, LoggerFactory.Create(_ => { }));

    /// <summary>The reconfigurer is called every time it is run, while there is a current user.</summary>
    [Fact]
    public async Task Run_WithACurrentUser_CallsTheReconfigurerEachTime()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        controller.SetupGet(c => c.Reconfigurer).Returns(reconfigurer.Object);
        ReconfigurationService service = Build();

        await service.Run();
        await service.Run();

        reconfigurer.Verify(r => r.OnReconfigured(), Times.Exactly(2));
    }

    /// <summary>With no current user the reconfigurer is never called, and is not even built.</summary>
    [Fact]
    public async Task Run_WithoutACurrentUser_NeverCallsTheReconfigurer()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns((UserInfo?)null);
        controller.SetupGet(c => c.Reconfigurer).Returns(reconfigurer.Object);

        await Build().Run();

        reconfigurer.Verify(r => r.OnReconfigured(), Times.Never);
        controller.VerifyGet(c => c.Reconfigurer, Times.Never);
    }

    /// <summary>Without a reconfigurer nothing happens.</summary>
    [Fact]
    public async Task Run_WithoutAReconfigurer_DoesNothing()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        controller.SetupGet(c => c.Reconfigurer).Returns((IReconfigurer?)null);

        await Build().Run();
    }

    /// <summary>A reconfigurer that throws, or cannot be built, does not throw back into the engine.</summary>
    [Fact]
    public async Task Run_AReconfigurerThatFails_IsLoggedNotThrown()
    {
        users.Setup(u => u.GetCurrentUserInfo()).Returns(new UserInfo { Name = "ME" });
        controller.SetupGet(c => c.Reconfigurer).Returns(reconfigurer.Object);
        reconfigurer.Setup(r => r.OnReconfigured()).ThrowsAsync(new InvalidOperationException("boom"));

        await Build().Run();

        controller.SetupGet(c => c.Reconfigurer).Throws(new InvalidOperationException("cannot build"));
        await Build().Run();
    }
}
