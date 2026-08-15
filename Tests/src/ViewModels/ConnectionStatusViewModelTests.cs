namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="ConnectionStatusViewModel"/>.</summary>
public sealed class ConnectionStatusViewModelTests
{
    private sealed class FakeStatusService : IConnectionStatusService
    {
        public event Action? StatusesChanged;

        public List<PeerConnectionStatus> Statuses { get; set; } = [];

        public IReadOnlyList<PeerConnectionStatus> GetStatuses() => Statuses;

        public void Raise() => StatusesChanged?.Invoke();
    }

    /// <summary>ServerRows and ClientRows are split from GetStatuses by Kind on construction.</summary>
    [Fact]
    public void Constructor_SplitsRowsByKind()
    {
        FakeStatusService service = new()
        {
            Statuses =
            [
                new PeerConnectionStatus { UserName = "SERVER-B", Kind = PeerConnectionKind.Server, IsConnected = true },
                new PeerConnectionStatus { UserName = "TEST1", Kind = PeerConnectionKind.Client, IsConnected = false }
            ]
        };

        ConnectionStatusViewModel vm = new(service);

        ConnectionRowViewModel serverRow = Assert.Single(vm.ServerRows);
        Assert.Equal("SERVER-B", serverRow.UserName);
        Assert.True(serverRow.IsConnected);
        Assert.True(vm.HasServerRows);

        ConnectionRowViewModel clientRow = Assert.Single(vm.ClientRows);
        Assert.Equal("TEST1", clientRow.UserName);
        Assert.False(clientRow.IsConnected);
        Assert.True(vm.HasClientRows);
    }

    /// <summary>ServerRows/ClientRows refresh from the latest GetStatuses snapshot whenever StatusesChanged fires.</summary>
    [Fact]
    public void StatusesChanged_Raised_RefreshesRows()
    {
        FakeStatusService service = new()
        {
            Statuses = [new PeerConnectionStatus { UserName = "SERVER-A", Kind = PeerConnectionKind.Server, IsConnected = false }]
        };
        ConnectionStatusViewModel vm = new(service);

        service.Statuses = [new PeerConnectionStatus { UserName = "SERVER-A", Kind = PeerConnectionKind.Server, IsConnected = true, LastConnectedAt = DateTime.UtcNow }];
        service.Raise();

        ConnectionRowViewModel row = Assert.Single(vm.ServerRows);
        Assert.True(row.IsConnected);
        Assert.NotNull(row.LastConnectedAt);
    }

    /// <summary>An empty status list (e.g. Peer mode's NullConnectionStatusService) produces no rows in either table.</summary>
    [Fact]
    public void Constructor_NoStatuses_BothRowCollectionsEmpty()
    {
        ConnectionStatusViewModel vm = new(new FakeStatusService());

        Assert.Empty(vm.ServerRows);
        Assert.Empty(vm.ClientRows);
        Assert.False(vm.HasServerRows);
        Assert.False(vm.HasClientRows);
    }

    /// <summary>HasServerRows/HasClientRows go false once a refresh drops every row of that kind.</summary>
    [Fact]
    public void StatusesChanged_AllRowsOfKindRemoved_HasRowsBecomesFalse()
    {
        FakeStatusService service = new()
        {
            Statuses = [new PeerConnectionStatus { UserName = "TEST1", Kind = PeerConnectionKind.Client, IsConnected = true }]
        };
        ConnectionStatusViewModel vm = new(service);
        Assert.True(vm.HasClientRows);

        service.Statuses = [];
        service.Raise();

        Assert.False(vm.HasClientRows);
        Assert.Empty(vm.ClientRows);
    }

    /// <summary>A refresh notifies HasServerRows and HasClientRows.</summary>
    [Fact]
    public void StatusesChanged_Raised_NotifiesHasRowsProperties()
    {
        FakeStatusService service = new();
        ConnectionStatusViewModel vm = new(service);
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        service.Raise();

        Assert.Contains("HasServerRows", changed);
        Assert.Contains("HasClientRows", changed);
    }
}
