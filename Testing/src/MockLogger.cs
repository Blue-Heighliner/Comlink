namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="ILogger"/> the engine offers, which records every entry written to it so a test can see what a component logged.</summary>
public sealed class MockLogger : Mock<ILogger>
{
    private readonly List<MockLogEntry> entries = [];

    /// <summary>Initializes the mock.</summary>
    public MockLogger()
    {
        Setup(logger => logger.Log(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Callback((string category, string id, string message) => entries.Add(new MockLogEntry(category, id, message, null)));
        Setup(logger => logger.Log(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<string>())).Callback((string category, string id, Exception exception, string message) => entries.Add(new MockLogEntry(category, id, message, exception)));
    }

    /// <summary>Gets every entry written so far, in the order written.</summary>
    public IReadOnlyList<MockLogEntry> Entries => entries;
}
