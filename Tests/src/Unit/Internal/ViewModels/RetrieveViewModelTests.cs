namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="RetrieveViewModel"/>.</summary>
public sealed class RetrieveViewModelTests
{
    private static (RetrieveViewModel Vm, Mock<IRetrievalService> Service) Build(params string[] servers)
    {
        Mock<IEngineController> controller = new();
        controller.Setup(c => c.StorageServers).Returns(servers);
        Mock<IRetrievalService> service = new();
        service.Setup(s => s.Request(It.IsAny<string>(), It.IsAny<RetrievalCriteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return (new RetrieveViewModel(controller.Object, service.Object), service);
    }

    /// <summary>The first configured storage server is selected by default.</summary>
    [Fact]
    public void Ctor_SelectsFirstStorageServer()
    {
        (RetrieveViewModel vm, _) = Build("S1", "S2");

        Assert.Equal(["S1", "S2"], vm.AvailableServers);
        Assert.Equal("S1", vm.SelectedServer);
    }

    /// <summary>With nothing entered, the request carries empty criteria for the selected server and reports success.</summary>
    [Fact]
    public async Task Request_NothingEntered_SendsEmptyCriteria()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build("S1");

        await vm.RequestCommand.ExecuteAsync(null);

        service.Verify(s => s.Request("S1", It.Is<RetrievalCriteria>(c => c.From == null && c.To == null && c.Authors.Count == 0 && c.Destinations.Count == 0 && c.Ids.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Requested from S1", vm.StatusMessage);
        Assert.False(vm.IsRequesting);
    }

    /// <summary>List boxes split on commas, semicolons and new lines, trimming and dropping blanks and repeats.</summary>
    [Fact]
    public async Task Request_SplitsListsOnCommasSemicolonsAndNewLines()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build("S1");
        vm.Authors = " ALICE , bob;\nalice ,, ";
        vm.Destinations = "CAROL";
        vm.Ids = "M1,M2";

        await vm.RequestCommand.ExecuteAsync(null);

        service.Verify(s => s.Request("S1", It.Is<RetrievalCriteria>(c => c.Authors.SequenceEqual(new[] { "ALICE", "bob" }) && c.Destinations.SequenceEqual(new[] { "CAROL" }) && c.Ids.SequenceEqual(new[] { "M1", "M2" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A lone date covers its whole day: midnight to the last instant, as local time converted to UTC.</summary>
    [Fact]
    public async Task Request_DatesOnly_CoverWholeDaysAsUtc()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build("S1");
        DateTime day = new(2026, 3, 4, 0, 0, 0, DateTimeKind.Local);
        vm.DateFrom = new DateTimeOffset(day);
        vm.DateTo = new DateTimeOffset(day);

        await vm.RequestCommand.ExecuteAsync(null);

        service.Verify(s => s.Request("S1", It.Is<RetrievalCriteria>(c =>
            c.From == day.ToUniversalTime() && c.To == day.AddDays(1).AddMilliseconds(-1).ToUniversalTime()), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A chosen time of day is used instead of the whole-day default.</summary>
    [Fact]
    public async Task Request_ExplicitTimes_AreUsed()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build("S1");
        DateTime day = new(2026, 3, 4, 0, 0, 0, DateTimeKind.Local);
        vm.DateFrom = new DateTimeOffset(day);
        vm.TimeFrom = TimeSpan.FromHours(9);
        vm.DateTo = new DateTimeOffset(day);
        vm.TimeTo = TimeSpan.FromHours(17);

        await vm.RequestCommand.ExecuteAsync(null);

        service.Verify(s => s.Request("S1", It.Is<RetrievalCriteria>(c =>
            c.From == day.AddHours(9).ToUniversalTime() && c.To == day.AddHours(17).ToUniversalTime()), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>With no storage server there is nothing to send to.</summary>
    [Fact]
    public async Task Request_NoServer_ReportsAndSendsNothing()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build();

        await vm.RequestCommand.ExecuteAsync(null);

        Assert.Equal("Select a server", vm.StatusMessage);
        service.Verify(s => s.Request(It.IsAny<string>(), It.IsAny<RetrievalCriteria>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A request the server side could not be reached for says so, and one that throws reports the failure.</summary>
    [Fact]
    public async Task Request_UnreachableOrFailing_ReportsIt()
    {
        (RetrieveViewModel vm, Mock<IRetrievalService> service) = Build("S1");
        service.Setup(s => s.Request(It.IsAny<string>(), It.IsAny<RetrievalCriteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await vm.RequestCommand.ExecuteAsync(null);
        Assert.Equal("Could not reach S1", vm.StatusMessage);

        service.Setup(s => s.Request(It.IsAny<string>(), It.IsAny<RetrievalCriteria>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        await vm.RequestCommand.ExecuteAsync(null);
        Assert.Equal("Retrieval failed: boom", vm.StatusMessage);
        Assert.False(vm.IsRequesting);
    }

    /// <summary>Reset clears every criterion but keeps the chosen server.</summary>
    [Fact]
    public void Reset_ClearsCriteriaKeepsServer()
    {
        (RetrieveViewModel vm, _) = Build("S1", "S2");
        vm.SelectedServer = "S2";
        vm.DateFrom = DateTimeOffset.Now;
        vm.TimeTo = TimeSpan.FromHours(1);
        vm.Authors = "A";
        vm.Destinations = "D";
        vm.Ids = "I";

        vm.ResetCommand.Execute(null);

        Assert.Null(vm.DateFrom);
        Assert.Null(vm.TimeTo);
        Assert.Equal((string.Empty, string.Empty, string.Empty), (vm.Authors, vm.Destinations, vm.Ids));
        Assert.Equal("S2", vm.SelectedServer);
    }
}
