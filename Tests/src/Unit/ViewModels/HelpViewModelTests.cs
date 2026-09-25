namespace BlueHeighliner.Comlink.Tests.Unit.ViewModels;

/// <summary>Unit tests for <see cref="HelpViewModel"/>, whose tabs reflect the configured role and compose settings.</summary>
public sealed class HelpViewModelTests
{
    private static HelpViewModel Build(NodeRole role = NodeRole.Peer, bool tags = true, bool alerts = true)
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.Role).Returns(role);
        controller.Setup(c => c.TagsEnabled).Returns(tags);
        controller.Setup(c => c.ComposeAlertsEnabled).Returns(alerts);
        controller.Setup(c => c.TagLabel).Returns("Subject Line");
        controller.Setup(c => c.AlertLabel).Returns("FLASH");
        controller.Setup(c => c.AppName).Returns("MyApp");
        return new HelpViewModel(controller.Object);
    }

    private static IEnumerable<string> Titles(HelpViewModel vm) => vm.Tabs.Select(t => t.Title);

    /// <summary>The window title uses the configured application name.</summary>
    [Fact]
    public void AppName_ComesFromEngineController() => Assert.Equal("MyApp", Build().AppName);

    /// <summary>A peer offers the full messaging guide, in the order a new user needs it, and no connection tab.</summary>
    [Fact]
    public void Peer_HasMessagingTabsAndNoConnectionTab()
    {
        Assert.Equal(
            ["Getting started", "Sending a message", "Receiving messages", "Notes and drafts", "Folders and entries", "Backup and restore", "Printing"],
            Titles(Build(NodeRole.Peer)));
    }

    /// <summary>A client has the messaging guide plus a tab about its connection to the server.</summary>
    [Fact]
    public void Client_AddsConnectionTab()
    {
        HelpViewModel vm = Build(NodeRole.Client);

        Assert.Equal("Connection", vm.Tabs[^1].Title);
        Assert.Contains("Getting started", Titles(vm));
    }

    /// <summary>A server has no inbox or drafts, so it gets a guide to its own two views instead of the messaging tabs.</summary>
    [Fact]
    public void Server_HasOnlyServerTabs()
    {
        HelpViewModel vm = Build(NodeRole.Server);

        Assert.Equal(["Overview", "Connections", "Activity"], Titles(vm));
    }

    /// <summary>Every tab has content, and every section has a heading and text.</summary>
    [Theory]
    [InlineData(NodeRole.Peer)]
    [InlineData(NodeRole.Client)]
    [InlineData(NodeRole.Server)]
    public void EveryTab_HasNonEmptySections(NodeRole role)
    {
        foreach (HelpTab tab in Build(role).Tabs)
        {
            Assert.NotEmpty(tab.Sections);
            Assert.All(tab.Sections, section =>
            {
                Assert.False(string.IsNullOrWhiteSpace(section.Heading));
                Assert.False(string.IsNullOrWhiteSpace(section.Body));
            });
        }
    }

    /// <summary>Tab titles are unique so the tab strip is unambiguous.</summary>
    [Theory]
    [InlineData(NodeRole.Peer)]
    [InlineData(NodeRole.Client)]
    [InlineData(NodeRole.Server)]
    public void TabTitles_AreUnique(NodeRole role)
        => Assert.Equal(Titles(Build(role)).Count(), Titles(Build(role)).Distinct().Count());

    /// <summary>The tag section appears, named with the host's own label, only when tags are enabled.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sending_TagSection_FollowsTagsEnabled(bool tags)
    {
        HelpTab sending = Build(tags: tags).Tabs.Single(t => t.Title == "Sending a message");

        Assert.Equal(tags, sending.Sections.Any(s => s.Heading == "Subject Line"));
    }

    /// <summary>The alert compose section appears, named with the host's own label, only when composing alerts is enabled.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sending_AlertSection_FollowsComposeAlertsEnabled(bool alerts)
    {
        HelpTab sending = Build(alerts: alerts).Tabs.Single(t => t.Title == "Sending a message");

        Assert.Equal(alerts, sending.Sections.Any(s => s.Heading == "Sending as FLASH"));
    }

    /// <summary>The receiving tab names alerts with the host's own label, so it matches what the title bar shows.</summary>
    [Fact]
    public void Receiving_AlertSection_UsesAlertLabel()
    {
        HelpTab receiving = Build().Tabs.Single(t => t.Title == "Receiving messages");

        Assert.Contains(receiving.Sections, s => s.Heading == "FLASH");
    }
}
