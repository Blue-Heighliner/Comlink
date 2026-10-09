namespace BlueHeighliner.Comlink;

/// <summary>An auto forwarder added via <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.AutoForwarder"/>.</summary>
internal sealed record AutoForwarderDefinition
{
    /// <summary>Gets the display name shown for this auto forwarder in the client's auto forward screen, and its key in local target-list storage.</summary>
    public required string Name { get; init; }
}
