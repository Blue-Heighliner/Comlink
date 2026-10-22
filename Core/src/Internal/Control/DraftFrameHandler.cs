namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/>.</summary>
internal interface IDraftFrameHandler
{
    /// <summary>Gets how wide a line of a new draft is, or <see langword="null"/> for no limit.</summary>
    int? DefaultLineWidth { get; }
    /// <summary>Gets the narrowest a line may be.</summary>
    int MinLineWidth { get; }
    /// <summary>Gets the widest a line may be, or <see langword="null"/> for no maximum.</summary>
    int? MaxLineWidth { get; }
    /// <summary>Gets a value indicating whether messages carry a tag.</summary>
    bool EnableTags { get; }
    /// <summary>Gets what message tags may be.</summary>
    TagRules TagRules { get; }
    /// <summary>Gets whether a message must have an aspect.</summary>
    bool IsAspectRequired { get; }
    /// <summary>Gets the tag a new draft starts with, or <see langword="null"/> for none.</summary>
    string? DefaultTag { get; }
    /// <summary>Gets the priority a new draft starts at as the host's enum member, or <see langword="null"/> for the lowest the user may choose.</summary>
    Enum? DefaultPriority { get; }
    /// <summary>Gets the level a new draft starts at as the host's enum member, or <see langword="null"/> for the highest the user may use.</summary>
    Enum? DefaultLevel { get; }
    /// <summary>Gets the aspect a new draft starts with as the host's enum member, or <see langword="null"/> for none.</summary>
    Enum? DefaultAspect { get; }
    /// <summary>Returns why a draft may not have this combination, or <see langword="null"/> when it may.</summary>
    /// <param name="priority">The priority as the host's enum member.</param>
    /// <param name="level">The level as the host's enum member, or <see langword="null"/> for none.</param>
    /// <param name="aspect">The aspect as the host's enum member, or <see langword="null"/> for none.</param>
    /// <param name="tag">The tag, empty for none.</param>
    string? IsBlocked(Enum priority, Enum? level, Enum? aspect, string tag);
    /// <summary>Returns whether a message sent from the draft in <paramref name="content"/> is an alert.</summary>
    /// <param name="content">The draft as it currently is.</param>
    bool IsAlert(DraftContent content);
    /// <summary>Returns the identifier of the next message sent, given the one generated last, or <see langword="null"/> when none was.</summary>
    /// <param name="previous">The identifier generated last.</param>
    string NextId(string? previous);
    /// <summary>Returns the header a message sent from the draft in <paramref name="content"/> must start with, or <see langword="null"/> for none.</summary>
    /// <param name="content">The draft as it currently is.</param>
    string? GetHeader(DraftContent content);
}

/// <summary>Adapts a typed <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> to <see cref="IDraftFrameHandler"/>.</summary>
internal sealed class DraftFrameHandler<TPriority, TLevel, TAspect>(IDraftHandler<TPriority, TLevel, TAspect> handler) : IDraftFrameHandler where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public int? DefaultLineWidth => handler.DefaultLineWidth;

    /// <inheritdoc />
    public int MinLineWidth => handler.MinLineWidth;

    /// <inheritdoc />
    public int? MaxLineWidth => handler.MaxLineWidth;

    /// <inheritdoc />
    public bool EnableTags => handler.EnableTags;

    /// <inheritdoc />
    public TagRules TagRules => new(handler.TagCase, Math.Max(0, handler.MinTagLength), handler.MaxTagLength, handler.AllowTagSymbols, handler.AllowTagNumbers, handler.AllowTagSpaces, handler.IsTagRequired);

    /// <inheritdoc />
    public bool IsAspectRequired => handler.IsAspectRequired;

    /// <inheritdoc />
    public string? DefaultTag => handler.DefaultTag;

    /// <inheritdoc />
    public Enum? DefaultPriority => handler.DefaultPriority is { } priority ? priority : null;

    /// <inheritdoc />
    public Enum? DefaultLevel => handler.DefaultLevel is { } level ? level : null;

    /// <inheritdoc />
    public Enum? DefaultAspect => handler.DefaultAspect is { } aspect ? aspect : null;

    /// <inheritdoc />
    public string? IsBlocked(Enum priority, Enum? level, Enum? aspect, string tag)
        => handler.IsBlocked(new DraftBlockContext<TPriority, TLevel, TAspect> { Tag = tag, Priority = (TPriority)(object)priority, Level = level is null ? null : (TLevel)(object)level, Aspect = aspect is null ? null : (TAspect)(object)aspect });

    /// <inheritdoc />
    public bool IsAlert(DraftContent content) => handler.IsAlert(ToState(content));

    /// <inheritdoc />
    public string NextId(string? previous) => handler.NextId(previous);

    /// <inheritdoc />
    public string? GetHeader(DraftContent content) => handler.GetHeader(ToState(content));

    private DraftContext<TPriority, TLevel, TAspect> ToState(DraftContent content)
        => new()
        {
            Tag = content.Tag,
            Priority = (TPriority)(object)content.Priority,
            Level = content.Level is TLevel level ? level : null,
            Aspect = content.Aspect is TAspect aspect ? aspect : null,
            Body = content.Body,
            Addresses = content.Addresses,
            LineWidth = content.LineWidth
        };
}
