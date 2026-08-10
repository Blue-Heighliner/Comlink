namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="EntryItemViewModel"/> computed properties.</summary>
public sealed class EntryItemViewModelTests
{
    private static EntryItemViewModel Make(
        string? fixedStatus = null,
        DestinationStatus? overallStatus = null)
    {
        EntryItemViewModel vm = new("id", "Title", EntryType.Message, DateTime.UtcNow,
            fixedStatusText: fixedStatus);
        vm.OverallStatus = overallStatus;
        return vm;
    }

    // ── StatusText ────────────────────────────────────────────────────────────

    /// <summary>StatusText reflects OverallStatus when set.</summary>
    [Theory]
    [InlineData(DestinationStatus.Confirmed, "CONFIRMED")]
    [InlineData(DestinationStatus.Failed,    "FAILED")]
    [InlineData(DestinationStatus.Sent,      "SENT")]
    public void StatusText_WithOverallStatus_ReturnsUppercaseName(DestinationStatus status, string expected)
    {
        EntryItemViewModel vm = Make(overallStatus: status);
        Assert.Equal(expected, vm.StatusText);
    }

    /// <summary>StatusText falls back to FixedStatusText when OverallStatus is null.</summary>
    [Fact]
    public void StatusText_OverallStatusNull_ReturnsFixedStatusText()
    {
        EntryItemViewModel vm = Make(fixedStatus: "RECEIVED");
        Assert.Equal("RECEIVED", vm.StatusText);
    }

    /// <summary>StatusText is null when both OverallStatus and FixedStatusText are null.</summary>
    [Fact]
    public void StatusText_BothNull_ReturnsNull()
    {
        EntryItemViewModel vm = Make();
        Assert.Null(vm.StatusText);
    }

    /// <summary>OverallStatus takes priority over FixedStatusText.</summary>
    [Fact]
    public void StatusText_BothSet_OverallStatusWins()
    {
        EntryItemViewModel vm = Make(fixedStatus: "RECEIVED", overallStatus: DestinationStatus.Confirmed);
        Assert.Equal("CONFIRMED", vm.StatusText);
    }

    // ── Constructor properties ────────────────────────────────────────────────

    /// <summary>Constructor assigns all properties correctly.</summary>
    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        DateTime sortDate = new(2025, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        EntryItemViewModel vm = new("msg123", "Hello", EntryType.Draft, sortDate,
            secondaryText: "from ALPHA", timeText: "12:00", fixedStatusText: "DRAFT");

        Assert.Equal("msg123", vm.Id);
        Assert.Equal("Hello", vm.Title);
        Assert.Equal(EntryType.Draft, vm.EntryType);
        Assert.Equal(sortDate, vm.SortDate);
        Assert.Equal("from ALPHA", vm.SecondaryText);
        Assert.Equal("12:00", vm.TimeText);
        Assert.Equal("DRAFT", vm.FixedStatusText);
        Assert.False(vm.IsSelected);
        Assert.Null(vm.OverallStatus);
    }

    /// <summary>IsSelected is observable and changes notify.</summary>
    [Fact]
    public void IsSelected_Observable_ChangesRaiseNotification()
    {
        EntryItemViewModel vm = new("x", "T", EntryType.Note, DateTime.UtcNow);
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.IsSelected = true;

        Assert.Contains("IsSelected", changed);
    }

    /// <summary>OverallStatus change notifies StatusText and StatusColorHex.</summary>
    [Fact]
    public void OverallStatus_Change_NotifiesStatusTextAndColorHex()
    {
        EntryItemViewModel vm = new("x", "T", EntryType.Message, DateTime.UtcNow);
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.OverallStatus = DestinationStatus.Confirmed;

        Assert.Contains("StatusText", changed);
        Assert.Contains("StatusColorHex", changed);
    }
}
