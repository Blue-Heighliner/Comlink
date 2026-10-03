namespace BlueHeighliner.Comlink;

/// <summary>Marks a class that is constructed directly with arguments the container cannot supply, so the convention scan in <see cref="EngineExtensions"/> never registers it.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ConstructedManuallyAttribute : Attribute
{
}
