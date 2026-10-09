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
    /// <summary>Gets the tag a new draft starts with, or <see langword="null"/> for none.</summary>
    string? DefaultTag { get; }
    /// <summary>Gets the priority a new draft starts at as the host's enum member, or <see langword="null"/> for the lowest the user may choose.</summary>
    Enum? DefaultPriority { get; }
    /// <summary>Gets the message level a new draft starts at as the host's enum member, or <see langword="null"/> for the highest the user may use.</summary>
    Enum? DefaultMessageLevel { get; }
    /// <summary>Gets the message aspect a new draft starts with as the host's enum member, or <see langword="null"/> for none.</summary>
    Enum? DefaultMessageAspect { get; }
    /// <summary>Returns whether a draft may have this combination.</summary>
    /// <param name="context">A snapshot of the engine.</param>
    /// <param name="priority">The priority as the host's enum member.</param>
    /// <param name="level">The message level as the host's enum member, or <see langword="null"/> for none.</param>
    /// <param name="aspect">The message aspect as the host's enum member, or <see langword="null"/> for none.</param>
    /// <param name="tag">The tag, empty for none.</param>
    bool IsAllowed(IEngineContext context, Enum priority, Enum? level, Enum? aspect, string tag);
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
internal sealed class DraftFrameHandler<TPriority, TLevel, TAspect>(IDraftHandler<TPriority, TLevel, TAspect> handler, IReadOnlyList<MessageLevel> messageLevels, IReadOnlyList<MessageAspect> messageAspects) : IDraftFrameHandler where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
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
    public string? DefaultTag => handler.DefaultTag;

    /// <inheritdoc />
    public Enum? DefaultPriority => handler.DefaultPriority is { } priority ? priority : null;

    /// <inheritdoc />
    public Enum? DefaultMessageLevel => handler.DefaultMessageLevel is { } level ? level : null;

    /// <inheritdoc />
    public Enum? DefaultMessageAspect => handler.DefaultMessageAspect is { } aspect ? aspect : null;

    /// <inheritdoc />
    public bool IsAllowed(IEngineContext context, Enum priority, Enum? level, Enum? aspect, string tag)
        => handler.IsAllowed(context, (TPriority)(object)priority, level is null ? null : (TLevel)(object)level, aspect is null ? null : (TAspect)(object)aspect, tag);

    /// <inheritdoc />
    public bool IsAlert(DraftContent content) => handler.IsAlert(ToState(content));

    /// <inheritdoc />
    public string NextId(string? previous) => handler.NextId(previous);

    /// <inheritdoc />
    public string? GetHeader(DraftContent content) => handler.GetHeader(ToState(content));

    private DraftState<TPriority, TLevel, TAspect> ToState(DraftContent content)
        => new()
        {
            Tag = content.Tag,
            Priority = (TPriority)(object)content.Priority,
            MessageLevel = string.IsNullOrEmpty(content.MessageLevel) ? null : messageLevels.FirstOrDefault(level => string.Equals(level.Name, content.MessageLevel, StringComparison.OrdinalIgnoreCase))?.Key is TLevel key ? key : null,
            MessageAspect = string.IsNullOrEmpty(content.MessageAspect) ? null : messageAspects.FirstOrDefault(aspect => string.Equals(aspect.Name, content.MessageAspect, StringComparison.OrdinalIgnoreCase))?.Key is TAspect aspectKey ? aspectKey : null,
            Body = content.Body,
            Addresses = content.Addresses,
            LineWidth = content.LineWidth
        };
}
