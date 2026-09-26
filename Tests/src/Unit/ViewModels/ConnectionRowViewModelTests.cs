namespace BlueHeighliner.Comlink.Tests.Unit.ViewModels;

/// <summary>Unit tests for <see cref="ConnectionRowViewModel"/> computed properties.</summary>
public sealed class ConnectionRowViewModelTests
{
    /// <summary>The constructor assigns UserName and defaults every other property.</summary>
    [Fact]
    public void Constructor_AssignsUserNameAndDefaults()
    {
        ConnectionRowViewModel vm = new("SERVER-A");

        Assert.Equal("SERVER-A", vm.UserName);
        Assert.False(vm.IsConnected);
        Assert.Null(vm.LastConnectedAt);
        Assert.Null(vm.LastDisconnectedAt);
    }

    /// <summary>StatusText and StatusColorHex reflect IsConnected.</summary>
    [Theory]
    [InlineData(true, "UP", "#98C379")]
    [InlineData(false, "DN", "#E06C75")]
    public void StatusTextAndColor_ReflectIsConnected(bool isConnected, string expectedText, string expectedColor)
    {
        ConnectionRowViewModel vm = new("SERVER-A") { IsConnected = isConnected };

        Assert.Equal(expectedText, vm.StatusText);
        Assert.Equal(expectedColor, vm.StatusColorHex);
    }

    /// <summary>A closed row reads CLOSED in grey whether or not it is still marked connected.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StatusTextAndColor_WhenClosed_AreClosedAndGrey(bool isConnected)
    {
        ConnectionRowViewModel vm = new("SERVER-A") { IsConnected = isConnected, IsClosed = true };

        Assert.Equal("CLOSED", vm.StatusText);
        Assert.Equal("#ABB2BF", vm.StatusColorHex);
    }

    /// <summary>The toggle command asks to close an open row and to reopen a closed one, and its menu text says which.</summary>
    [Fact]
    public void ToggleClosedCommand_TogglesAndLabelsAccordingly()
    {
        List<bool> requests = [];
        ConnectionRowViewModel vm = new("SERVER-A", requests.Add);

        Assert.Equal("Close", vm.ToggleClosedText);
        vm.ToggleClosedCommand.Execute(null);
        vm.IsClosed = true;
        Assert.Equal("Open", vm.ToggleClosedText);
        vm.ToggleClosedCommand.Execute(null);

        Assert.Equal([true, false], requests);
    }

    /// <summary>Refresh runs the supplied action while open, and is disabled while closed.</summary>
    [Fact]
    public void RefreshCommand_RunsWhileOpen_DisabledWhileClosed()
    {
        int refreshes = 0;
        ConnectionRowViewModel vm = new("SERVER-A", null, () => refreshes++);
        bool canExecuteChanged = false;
        vm.RefreshCommand.CanExecuteChanged += (_, _) => canExecuteChanged = true;

        Assert.True(vm.RefreshCommand.CanExecute(null));
        vm.RefreshCommand.Execute(null);
        vm.IsClosed = true;

        Assert.Equal(1, refreshes);
        Assert.True(canExecuteChanged);
        Assert.False(vm.RefreshCommand.CanExecute(null));
    }

    /// <summary>A row built without actions has commands that simply do nothing.</summary>
    [Fact]
    public void Commands_WithoutActions_DoNothing()
    {
        ConnectionRowViewModel vm = new("SERVER-A");

        vm.ToggleClosedCommand.Execute(null);
        vm.RefreshCommand.Execute(null);

        Assert.False(vm.IsClosed);
    }

    /// <summary>LastConnectedText/LastDisconnectedText render an em dash when never set.</summary>
    [Fact]
    public void LastTimestampText_Null_RendersEmDash()
    {
        ConnectionRowViewModel vm = new("SERVER-A");

        Assert.Equal("—", vm.LastConnectedText);
        Assert.Equal("—", vm.LastDisconnectedText);
    }

    /// <summary>LastConnectedText formats a set timestamp as uppercase dd-MMM-yyyy HH:mm.</summary>
    [Fact]
    public void LastConnectedText_Set_FormatsUppercase()
    {
        ConnectionRowViewModel vm = new("SERVER-A") { LastConnectedAt = new DateTime(2026, 3, 5, 14, 30, 0, DateTimeKind.Utc) };

        Assert.Equal("05-MAR-2026 14:30", vm.LastConnectedText);
    }

    /// <summary>IsConnected change notifies StatusText and StatusColorHex.</summary>
    [Fact]
    public void IsConnected_Change_NotifiesStatusTextAndColorHex()
    {
        ConnectionRowViewModel vm = new("SERVER-A");
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.IsConnected = true;

        Assert.Contains("StatusText", changed);
        Assert.Contains("StatusColorHex", changed);
    }

    /// <summary>LastConnectedAt change notifies LastConnectedText; LastDisconnectedAt change notifies LastDisconnectedText.</summary>
    [Fact]
    public void LastTimestamps_Change_NotifyFormattedText()
    {
        ConnectionRowViewModel vm = new("SERVER-A");
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.LastConnectedAt = DateTime.UtcNow;
        vm.LastDisconnectedAt = DateTime.UtcNow;

        Assert.Contains("LastConnectedText", changed);
        Assert.Contains("LastDisconnectedText", changed);
    }
}
