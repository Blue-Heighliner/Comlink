namespace BlueHeighliner.Comlink.Testing;

/// <summary>A mock of the <see cref="IDraftContext{TPriority, TLevel, TAspect}"/> an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is told a draft is.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed class MockDraftContext<TPriority, TLevel, TAspect> : Mock<IDraftContext<TPriority, TLevel, TAspect>> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the mock.</summary>
    /// <param name="priority">The priority the draft is set to be sent at.</param>
    /// <param name="messageLevel">The message level the draft is set to be sent at, or <see langword="null"/> for none.</param>
    /// <param name="messageAspect">The message aspect the draft is set to be sent with, or <see langword="null"/> for none.</param>
    /// <param name="tag">The draft's tag, empty for none.</param>
    /// <param name="body">The draft's body text.</param>
    /// <param name="addresses">The draft's recipients, or <see langword="null"/> for none.</param>
    /// <param name="lineWidth">How many monospace characters wide a line of the draft is, or <see langword="null"/> for no limit.</param>
    public MockDraftContext(TPriority priority = default, TLevel? messageLevel = null, TAspect? messageAspect = null, string tag = "", string body = "", IReadOnlyList<AddressRequest>? addresses = null, int? lineWidth = null)
    {
        Priority = priority;
        MessageLevel = messageLevel;
        MessageAspect = messageAspect;
        Tag = tag;
        Body = body;
        Addresses = addresses ?? [];
        LineWidth = lineWidth;
        Setup(context => context.Priority).Returns(() => Priority);
        Setup(context => context.MessageLevel).Returns(() => MessageLevel);
        Setup(context => context.MessageAspect).Returns(() => MessageAspect);
        Setup(context => context.Tag).Returns(() => Tag);
        Setup(context => context.Body).Returns(() => Body);
        Setup(context => context.Addresses).Returns(() => Addresses);
        Setup(context => context.LineWidth).Returns(() => LineWidth);
    }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.Priority"/> returns.</summary>
    public TPriority Priority { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.MessageLevel"/> returns.</summary>
    public TLevel? MessageLevel { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.MessageAspect"/> returns.</summary>
    public TAspect? MessageAspect { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}.Tag"/> returns.</summary>
    public string Tag { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftContext{TPriority, TLevel, TAspect}.Body"/> returns.</summary>
    public string Body { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftContext{TPriority, TLevel, TAspect}.Addresses"/> returns.</summary>
    public IReadOnlyList<AddressRequest> Addresses { get; set; }

    /// <summary>Gets or sets what <see cref="IDraftContext{TPriority, TLevel, TAspect}.LineWidth"/> returns.</summary>
    public int? LineWidth { get; set; }
}
