namespace BlueHeighliner.Comlink;

/// <summary>LiteDB document holding one auto forwarder's locally-saved target list.</summary>
internal sealed class AutoForwardTargetsEntity
{
    /// <summary>The owning controller's name (see <see cref="AutoForwarderDefinition.Name"/>).</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>User names this controller currently forwards a matching received message to.</summary>
    public List<string> Targets { get; set; } = [];
}
