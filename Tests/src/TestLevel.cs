namespace BlueHeighliner.Comlink.Tests;

/// <summary>The message levels the test configuration states, lowest first, covering the names tests send messages at.</summary>
public enum TestLevel
{
    /// <summary>The lowest level.</summary>
    Public,

    /// <summary>The second level.</summary>
    Internal,

    /// <summary>The third level.</summary>
    Restricted,

    /// <summary>The fourth level.</summary>
    Secret,

    /// <summary>A level for tests that need a plain low level.</summary>
    Low,

    /// <summary>The highest level.</summary>
    High
}
