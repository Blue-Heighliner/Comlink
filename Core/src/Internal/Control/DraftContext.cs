namespace BlueHeighliner.Comlink;

/// <summary>The engine's implementation of the combination a host's <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is asked to judge.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects.</typeparam>
internal sealed record DraftBlockContext<TPriority, TLevel, TAspect> : IDraftBlockContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public required string Tag { get; init; }

    /// <inheritdoc />
    public required TPriority Priority { get; init; }

    /// <inheritdoc />
    public required TLevel? MessageLevel { get; init; }

    /// <inheritdoc />
    public TAspect? MessageAspect { get; init; }
}

/// <summary>The engine's implementation of a draft as a host's <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is told about it.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects.</typeparam>
internal sealed record DraftContext<TPriority, TLevel, TAspect> : IDraftContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public required string Tag { get; init; }

    /// <inheritdoc />
    public required TPriority Priority { get; init; }

    /// <inheritdoc />
    public required TLevel? MessageLevel { get; init; }

    /// <inheritdoc />
    public TAspect? MessageAspect { get; init; }

    /// <inheritdoc />
    public required string Body { get; init; }

    /// <inheritdoc />
    public required IReadOnlyList<AddressRequest> Addresses { get; init; }

    /// <inheritdoc />
    public required int? LineWidth { get; init; }
}
