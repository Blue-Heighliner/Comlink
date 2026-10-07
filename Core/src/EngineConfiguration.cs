namespace BlueHeighliner.Comlink;

/// <summary>
/// Describes how the engine runs. A host implements this and names it to <see cref="Engine.Start{TConfiguration}"/>,
/// which constructs it through dependency injection, so its constructor may take services; the engine
/// calls <see cref="Configure"/> once, before anything else starts, and everything the host wants to differ from the
/// engine's defaults is stated through the fluent calls on the <see cref="IEngineBuilder"/> it is given. The first
/// thing a host must state is its types, through <see cref="IEngineBuilder.Types{TFrame, TPriority, TLevel, TAspect}"/> and <c>Frames</c>. See
/// <c>Docs/Api.md</c> and <c>Docs/Components/Configuration.md</c>.
/// </summary>
public interface IEngineConfiguration
{
    /// <summary>Configures the engine through <paramref name="engine"/>.</summary>
    /// <param name="engine">The builder to state the host's choices on.</param>
    void Configure(IEngineBuilder engine);
}
