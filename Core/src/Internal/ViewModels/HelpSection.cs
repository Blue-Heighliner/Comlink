namespace BlueHeighliner.Comlink;

/// <summary>A headed paragraph within a <see cref="HelpTab"/>.</summary>
/// <param name="Heading">The short heading shown above the paragraph.</param>
/// <param name="Body">The paragraph text.</param>
internal sealed record HelpSection(string Heading, string Body);
