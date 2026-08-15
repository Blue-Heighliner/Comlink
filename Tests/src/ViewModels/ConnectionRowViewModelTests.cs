namespace BlueHeighliner.Comlink.Tests.ViewModels;

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
