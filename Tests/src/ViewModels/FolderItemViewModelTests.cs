namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="FolderItemViewModel"/> computed properties.</summary>
public sealed class FolderItemViewModelTests
{
    // ── Root folder properties ────────────────────────────────────────────────

    /// <summary>Root folders have IsRootFolder=true, IsSubfolder=false, IsExpanded=true.</summary>
    [Fact]
    public void RootFolder_PropertiesAreCorrect()
    {
        FolderItemViewModel vm = new("root", "Inbox", FolderType.Inbox);

        Assert.True(vm.IsRootFolder);
        Assert.False(vm.IsSubfolder);
        Assert.True(vm.IsExpanded);
    }

    /// <summary>Subfolders have IsRootFolder=false, IsSubfolder=true, IsExpanded=false.</summary>
    [Fact]
    public void Subfolder_PropertiesAreCorrect()
    {
        FolderItemViewModel vm = new("child", "Sub", FolderType.Inbox, "root");

        Assert.False(vm.IsRootFolder);
        Assert.True(vm.IsSubfolder);
        Assert.False(vm.IsExpanded);
    }

    // ── Icon ──────────────────────────────────────────────────────────────────

    /// <summary>Root folder icons match the defined glyphs.</summary>
    [Theory]
    [InlineData(FolderType.Inbox,    "↓")]
    [InlineData(FolderType.Outbox,   "↑")]
    [InlineData(FolderType.Drafts,   "✎")]
    [InlineData(FolderType.Notes,    "☰")]
    [InlineData(FolderType.Activity, "≡")]
    public void RootFolder_Icon_MatchesType(FolderType type, string expectedIcon)
    {
        FolderItemViewModel vm = new("id", type.ToString(), type);
        Assert.Equal(expectedIcon, vm.Icon);
    }

    /// <summary>Subfolders always have an empty icon string.</summary>
    [Fact]
    public void Subfolder_Icon_IsEmpty()
    {
        FolderItemViewModel vm = new("child", "Sub", FolderType.Inbox, "root");
        Assert.Equal(string.Empty, vm.Icon);
    }

    // ── CanCreateSubfolder ────────────────────────────────────────────────────

    /// <summary>Activity root folders cannot create subfolders.</summary>
    [Fact]
    public void RootActivity_CannotCreateSubfolder()
    {
        FolderItemViewModel vm = new("act", "Activity", FolderType.Activity);
        Assert.False(vm.CanCreateSubfolder);
    }

    /// <summary>Outbox root folders cannot create subfolders.</summary>
    [Fact]
    public void RootOutbox_CannotCreateSubfolder()
    {
        FolderItemViewModel vm = new("out", "Outbox", FolderType.Outbox);
        Assert.False(vm.CanCreateSubfolder);
    }

    /// <summary>Non-Activity/Outbox root folders can create subfolders.</summary>
    [Theory]
    [InlineData(FolderType.Inbox)]
    [InlineData(FolderType.Drafts)]
    [InlineData(FolderType.Notes)]
    public void RootFolder_NonActivityNonOutbox_CanCreateSubfolder(FolderType type)
    {
        FolderItemViewModel vm = new("id", type.ToString(), type);
        Assert.True(vm.CanCreateSubfolder);
    }

    /// <summary>Subfolders can always create subfolders (regardless of type).</summary>
    [Fact]
    public void Subfolder_CanAlwaysCreateSubfolder()
    {
        FolderItemViewModel vm = new("child", "Sub", FolderType.Activity, "parent");
        Assert.True(vm.CanCreateSubfolder);
    }

    // ── Label style ───────────────────────────────────────────────────────────

    /// <summary>Root folders are bold and use 15pt size.</summary>
    [Fact]
    public void RootFolder_LabelStyle_IsBoldAndLarge()
    {
        FolderItemViewModel vm = new("root", "Inbox", FolderType.Inbox);
        Assert.True(vm.IsLabelBold);
        Assert.Equal(15.0, vm.LabelFontSize);
    }

    /// <summary>Subfolders are not bold and use 13pt size.</summary>
    [Fact]
    public void Subfolder_LabelStyle_IsNormalAndSmall()
    {
        FolderItemViewModel vm = new("child", "Sub", FolderType.Inbox, "root");
        Assert.False(vm.IsLabelBold);
        Assert.Equal(13.0, vm.LabelFontSize);
    }

    // ── ObservableObject ──────────────────────────────────────────────────────

    /// <summary>IsExpanded and IsSelected are observable.</summary>
    [Fact]
    public void ObservableProperties_RaisePropertyChanged()
    {
        FolderItemViewModel vm = new("root", "Inbox", FolderType.Inbox);
        List<string> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        vm.IsExpanded = false;
        vm.IsSelected = true;

        Assert.Contains("IsExpanded", changed);
        Assert.Contains("IsSelected", changed);
    }
}
