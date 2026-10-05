namespace BlueHeighliner.Comlink;

/// <summary>How wide a draft's lines may be, as the host's draft handler states it.</summary>
/// <param name="Default">How wide a line of a new draft is, or <see langword="null"/> for no limit.</param>
/// <param name="Min">The narrowest a line may be.</param>
/// <param name="Max">The widest a line may be, or <see langword="null"/> for no maximum.</param>
internal sealed record LineWidthRange(int? Default, int Min, int? Max)
{
    /// <summary>Gets the width a new draft starts at: the default, or the maximum when there is none (no limit would exceed it), or <see langword="null"/> for no limit.</summary>
    public int? Initial => Default is { } width ? Clamp(width) : Max;

    /// <summary>Returns <paramref name="width"/> within the minimum and maximum.</summary>
    /// <param name="width">The width.</param>
    public int Clamp(int width) => Math.Min(Math.Max(width, Min), Max ?? int.MaxValue);
}
