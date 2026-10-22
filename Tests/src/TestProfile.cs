namespace BlueHeighliner.Comlink.Tests;

/// <summary>A user profile record for tests that exercise typed profiles.</summary>
/// <param name="Desk">Where the user sits.</param>
/// <param name="Floor">Which floor it is on.</param>
public sealed record TestProfile(string Desk, int Floor);
