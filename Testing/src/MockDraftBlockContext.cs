namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}"/> an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is asked to judge.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockDraftBlockContext<TPriority, TLevel, TAspect> : Mock<IDraftBlockContext<TPriority, TLevel, TAspect>> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="priority">The priority the draft would be sent at.</param>
    /// <param name="messageLevel">The message level the draft would be sent at, or <see langword="null"/> for none.</param>
    /// <param name="messageAspect">The message aspect the draft would carry, or <see langword="null"/> for none.</param>
    /// <param name="tag">The tag the draft would have, empty for none.</param>
    public MockDraftBlockContext(TPriority priority = default, TLevel? messageLevel = null, TAspect? messageAspect = null, string tag = "")
    {
        Priority = priority;
        MessageLevel = messageLevel;
        MessageAspect = messageAspect;
        Tag = tag;
        Setup(context => context.Priority).Returns(() => Priority);
        Setup(context => context.MessageLevel).Returns(() => MessageLevel);
        Setup(context => context.MessageAspect).Returns(() => MessageAspect);
        Setup(context => context.Tag).Returns(() => Tag);
    }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.Priority"/> returns.</summary>
    public TPriority Priority { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.MessageLevel"/> returns.</summary>
    public TLevel? MessageLevel { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.MessageAspect"/> returns.</summary>
    public TAspect? MessageAspect { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.Tag"/> returns.</summary>
    public string Tag { get; set; }
}
