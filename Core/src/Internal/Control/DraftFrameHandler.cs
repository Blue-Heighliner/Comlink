namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IDraftHandler{TPriority, TLevel}"/>.</summary>
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
    /// <summary>Gets the security level a new draft starts at as the host's enum member, or <see langword="null"/> for the highest the user may use.</summary>
    Enum? DefaultSecurityLevel { get; }
    /// <summary>Returns the header a message sent from the draft in <paramref name="content"/> must start with, or <see langword="null"/> for none.</summary>
    /// <param name="content">The draft as it currently is.</param>
    string? GetHeader(DraftContent content);
}

/// <summary>Adapts a typed <see cref="IDraftHandler{TPriority, TLevel}"/> to <see cref="IDraftFrameHandler"/>.</summary>
internal sealed class DraftFrameHandler<TPriority, TLevel>(IDraftHandler<TPriority, TLevel> handler, IReadOnlyList<SecurityLevel> securityLevels) : IDraftFrameHandler where TPriority : struct, Enum where TLevel : struct, Enum
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
    public Enum? DefaultSecurityLevel => handler.DefaultSecurityLevel is { } level ? level : null;

    /// <inheritdoc />
    public string? GetHeader(DraftContent content)
        => handler.GetHeader(new DraftState<TPriority, TLevel>
        {
            Tag = content.Tag,
            Priority = (TPriority)(object)content.Priority,
            SecurityLevel = string.IsNullOrEmpty(content.SecurityLevel) ? null : securityLevels.FirstOrDefault(level => string.Equals(level.Name, content.SecurityLevel, StringComparison.OrdinalIgnoreCase))?.Key is TLevel key ? key : null,
            IsAlert = content.IsAlert,
            Addresses = content.Addresses,
            LineWidth = content.LineWidth
        });
}
