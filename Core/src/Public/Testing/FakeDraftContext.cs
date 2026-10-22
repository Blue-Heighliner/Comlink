namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDraftContext{TPriority, TLevel, TAspect}"/> an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is handed for a draft, to test its alert and header decisions without the engine.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
/// <param name="priority">The priority of the draft.</param>
/// <param name="messageLevel">The message level of the draft, or <see langword="null"/> for none.</param>
/// <param name="messageAspect">The message aspect of the draft, or <see langword="null"/> for none.</param>
/// <param name="tag">The tag of the draft, empty for none.</param>
/// <param name="body">The body of the draft.</param>
/// <param name="addresses">The recipients of the draft, or <see langword="null"/> for none.</param>
/// <param name="lineWidth">The width the draft is shown at, or <see langword="null"/> for no limit.</param>
public sealed class FakeDraftContext<TPriority, TLevel, TAspect>(TPriority priority = default, TLevel? messageLevel = null, TAspect? messageAspect = null, string tag = "", string body = "", IReadOnlyList<AddressRequest>? addresses = null, int? lineWidth = null) : IDraftContext<TPriority, TLevel, TAspect>
    where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public TPriority Priority { get; set; } = priority;

    /// <inheritdoc />
    public TLevel? MessageLevel { get; set; } = messageLevel;

    /// <inheritdoc />
    public TAspect? MessageAspect { get; set; } = messageAspect;

    /// <inheritdoc />
    public string Tag { get; set; } = tag;

    /// <inheritdoc />
    public string Body { get; set; } = body;

    /// <inheritdoc />
    public IReadOnlyList<AddressRequest> Addresses { get; set; } = addresses ?? [];

    /// <inheritdoc />
    public int? LineWidth { get; set; } = lineWidth;
}
