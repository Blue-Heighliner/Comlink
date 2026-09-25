namespace BlueHeighliner.Comlink.ViewModels;

/// <summary>One tab of the help guide, covering one way of using the application.</summary>
/// <param name="Title">The tab header.</param>
/// <param name="Sections">The headed paragraphs shown in the tab, in order.</param>
public sealed record HelpTab(string Title, IReadOnlyList<HelpSection> Sections);
