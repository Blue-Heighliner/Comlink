namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="InstallViewModel"/>.</summary>
public sealed class InstallViewModelTests
{
    private static SiteInfo MakeSiteInfo(string name) => new()
    {
        Name = name,
        Code = "CODE1",
        EnvironmentTitle = "PROD",
        EnvironmentColor = "#FF0000"
    };

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>Empty SiteCode sets ErrorMessage without calling the service.</summary>
    [Fact]
    public async Task Install_EmptySiteCode_SetsErrorMessage()
    {
        Mock<IServiceConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object);
        vm.SiteCode = "";

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        connMock.Verify(c => c.InstallSite(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Success ───────────────────────────────────────────────────────────────

    /// <summary>Valid code fires InstallSucceeded with the returned SiteInfo.</summary>
    [Fact]
    public async Task Install_ValidCode_FiresInstallSucceededWithSiteInfo()
    {
        SiteInfo expectedInfo = MakeSiteInfo("ALPHA");
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallSite("CODE1", It.IsAny<CancellationToken>())).ReturnsAsync(expectedInfo);
        InstallViewModel vm = new(connMock.Object);
        vm.SiteCode = "CODE1";

        SiteInfo? received = null;
        vm.InstallSucceeded += info => { received = info; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.Equal("ALPHA", received?.Name);
        Assert.Null(vm.ErrorMessage);
    }

    // ── Invalid code ──────────────────────────────────────────────────────────

    /// <summary>Unrecognised code (null result) sets ErrorMessage and does not fire event.</summary>
    [Fact]
    public async Task Install_InvalidCode_SetsErrorMessage()
    {
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallSite(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((SiteInfo?)null);
        InstallViewModel vm = new(connMock.Object);
        vm.SiteCode = "BADCODE";

        bool eventFired = false;
        vm.InstallSucceeded += _ => { eventFired = true; return Task.CompletedTask; };

        await vm.InstallCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.False(eventFired);
    }

    // ── IsLoading lifecycle ───────────────────────────────────────────────────

    /// <summary>IsLoading is true during install and false after completion.</summary>
    [Fact]
    public async Task Install_IsLoadingLifecycle()
    {
        TaskCompletionSource<SiteInfo?> gate = new();
        Mock<IServiceConnection> connMock = new();
        connMock.Setup(c => c.InstallSite(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        InstallViewModel vm = new(connMock.Object);
        vm.SiteCode = "CODE1";

        Task installTask = vm.InstallCommand.ExecuteAsync(null);
        Assert.True(vm.IsLoading);

        gate.SetResult(null);
        await installTask;
        Assert.False(vm.IsLoading);
    }

    // ── Auto-uppercase ────────────────────────────────────────────────────────

    /// <summary>SiteCode is automatically uppercased when set.</summary>
    [Fact]
    public void SiteCode_AutoUppercased()
    {
        Mock<IServiceConnection> connMock = new();
        InstallViewModel vm = new(connMock.Object);

        vm.SiteCode = "code1";

        Assert.Equal("CODE1", vm.SiteCode);
    }
}
