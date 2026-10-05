namespace BlueHeighliner.Comlink.Sample;

/// <summary>The "Escalation" auto forward controller, open to every Client scenario site, that forwards any received alert or <c>URGENT</c>-tagged message to whichever users its target list names.</summary>
public sealed class EscalationController : IAutoForwardController<Frame>
{
    /// <inheritdoc />
    public string Name { get; } = "Escalation";

    /// <inheritdoc />
    public IReadOnlyList<string> Users { get; } = ["CLIENT1", "CLIENT2"];

    /// <inheritdoc />
    public bool Accepts(Frame frame) => string.Equals(frame.Category, "ALERT", StringComparison.OrdinalIgnoreCase) || string.Equals(frame.Category, "URGENT", StringComparison.OrdinalIgnoreCase);
}
