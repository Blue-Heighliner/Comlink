namespace BlueHeighliner.Comlink.Testing;

/// <summary>One entry written to a <see cref="MockLogger"/>.</summary>
/// <param name="Category">The category it was written under, as given.</param>
/// <param name="Id">The identifier of the event it reported, as given.</param>
/// <param name="Message">What was written.</param>
/// <param name="Exception">The exception it reported, or <see langword="null"/> for none.</param>
public sealed record MockLogEntry(string Category, string Id, string Message, Exception? Exception);
