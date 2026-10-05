namespace BlueHeighliner.Comlink;

/// <summary>The recipients of a draft that are of one address type, in the order they were added or moved to.</summary>
/// <param name="Label">The address type's display label, such as <c>To</c>.</param>
/// <param name="Items">The recipients of that type.</param>
internal sealed record AddressGroup(string Label, IReadOnlyList<AddressData> Items);
