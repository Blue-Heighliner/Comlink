namespace BlueHeighliner.Comlink.Tests;

/// <summary>Priority levels for tests of the message handler and priority configuration, lowest first; the first ten are the levels <see cref="TestEngineController"/> offers.</summary>
public enum TestMessagePriority
{
    /// <summary>The lowest level.</summary>
    Normal,
    /// <summary>Level 1.</summary>
    Level1,
    /// <summary>Level 2.</summary>
    Level2,
    /// <summary>Level 3.</summary>
    Level3,
    /// <summary>Level 4.</summary>
    Level4,
    /// <summary>Level 5.</summary>
    Level5,
    /// <summary>Level 6.</summary>
    Level6,
    /// <summary>Level 7.</summary>
    Level7,
    /// <summary>Level 8.</summary>
    Level8,
    /// <summary>Level 9.</summary>
    Level9,
    /// <summary>A level for tests that mock their own priorities.</summary>
    Routine,
    /// <summary>A level for tests that mock their own priorities.</summary>
    Flash,
    /// <summary>A level for tests that mock their own priorities.</summary>
    Receipt,
    /// <summary>A level for tests that mock their own priorities.</summary>
    Low,
    /// <summary>A level for tests that mock their own priorities.</summary>
    Medium,
    /// <summary>A level for tests that mock their own priorities.</summary>
    High
}
