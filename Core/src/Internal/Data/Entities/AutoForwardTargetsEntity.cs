namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document holding one auto forward controller's locally-saved target list.</summary>
internal sealed class AutoForwardTargetsEntity
{
    /// <summary>The owning controller's name (see <see cref="AutoForwardControllerDefinition.Name"/>).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>User names this controller currently forwards a matching received message to.</summary>
    public List<string> Targets { get; set; } = [];
}
