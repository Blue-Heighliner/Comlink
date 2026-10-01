namespace BlueHeighliner.Comlink.Control;

/// <summary>A custom auto forward controller added via <see cref="IFrameBuilder{TFrame}.AutoForward"/>.</summary>
internal sealed record AutoForwardControllerDefinition
{
    /// <summary>Display name shown for this controller in the client's auto forward screen, and its key in local target-list storage.</summary>
    public required string Name { get; init; }
    /// <summary>User names allowed to open this controller and maintain its target list.</summary>
    public required IReadOnlyList<string> Users { get; init; }
    /// <summary>Answers whether a received message should be auto-forwarded through this controller.</summary>
    public required Func<object, bool> Filter { get; init; }
}
