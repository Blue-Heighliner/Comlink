namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="InstallViewModel"/>.</summary>
public sealed class InstallViewModelTests
{
    private static UserInfo MakeUserInfo(string name) => new()
    {
        Name = name
    };

    /// <summary>Empty UserName sets ErrorMessage without calling the service.</summary>
    [Fact]
    public async Task Install_EmptyUserName_SetsErrorMessage()
    {
        Mock<IServiceConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object);
        vm.UserName = "";

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        connMock.Verify(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Valid name fires InstallSucceeded with the returned UserInfo.</summary>
    [Fact]
    public async Task Install_ValidName_FiresInstallSucceededWithUserInfo()
    {
        UserInfo expectedInfo = MakeUserInfo("ALPHA");
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallUser("USER1", It.IsAny<CancellationToken>())).ReturnsAsync(expectedInfo);
        InstallViewModel vm = new(connMock.Object);
        vm.UserName = "USER1";

        UserInfo? received = null;
        vm.InstallSucceeded += info => { received = info; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal("ALPHA", received?.Name);
        Assert.Null(vm.ErrorMessage);
    }

    /// <summary>Unknown name (null result) sets ErrorMessage and does not fire event.</summary>
    [Fact]
    public async Task Install_UnknownName_SetsErrorMessage()
    {
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserInfo?)null);
        InstallViewModel vm = new(connMock.Object);
        vm.UserName = "NOBODY";

        bool eventFired = false;
        vm.InstallSucceeded += _ => { eventFired = true; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(eventFired);
    }

    /// <summary>IsLoading is true during install and false after completion.</summary>
    [Fact]
    public async Task Install_IsLoadingLifecycle()
    {
        TaskCompletionSource<UserInfo?> gate = new();
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallUser(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        InstallViewModel vm = new(connMock.Object);
        vm.UserName = "USER1";

        Task installTask = vm.InstallCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoading);

        gate.SetResult(null);
        await installTask;
        Assert.False(vm.IsLoading);
    }

    /// <summary>UserName is automatically uppercased when set.</summary>
    [Fact]
    public void UserName_AutoUppercased()
    {
        Mock<IServiceConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object);

        vm.UserName = "user1";

        Assert.Equal("USER1", vm.UserName);
    }
}
