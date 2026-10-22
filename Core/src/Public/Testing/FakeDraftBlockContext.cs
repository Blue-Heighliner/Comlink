namespace BlueHeighliner.Comlink.Testing;

/// <summary>A stand-in for the <see cref="IDraftBlockContext{TPriority, TLevel, TAspect}"/> an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is asked about, to test its blocking decisions without the engine.</summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its aspects.</typeparam>
/// <param name="priority">The priority of the combination.</param>
/// <param name="level">The level of the combination, or <see langword="null"/> for none.</param>
/// <param name="aspect">The aspect of the combination, or <see langword="null"/> for none.</param>
/// <param name="tag">The tag of the combination, empty for none.</param>
public sealed class FakeDraftBlockContext<TPriority, TLevel, TAspect>(TPriority priority = default, TLevel? level = null, TAspect? aspect = null, string tag = "") : IDraftBlockContext<TPriority, TLevel, TAspect>
    where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public TPriority Priority { get; set; } = priority;

    /// <inheritdoc />
    public TLevel? Level { get; set; } = level;

    /// <inheritdoc />
    public TAspect? Aspect { get; set; } = aspect;

    /// <inheritdoc />
    public string Tag { get; set; } = tag;
}
