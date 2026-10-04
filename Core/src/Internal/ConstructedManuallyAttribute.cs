namespace BlueHeighliner.Comlink;

/// <summary>Marks a class that is constructed directly where it is used, with arguments the container cannot supply or as one instance per use, so the convention scan in <see cref="EngineExtensions"/> never registers it.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ConstructedManuallyAttribute : Attribute
{
}
