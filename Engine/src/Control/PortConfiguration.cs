namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides TCP port numbers for the peer-to-peer and interface listeners.</summary>
public interface IPortConfiguration
{
    /// <summary>Port for the inbound interface listener. Always active, regardless of mode.</summary>
    int InterfacePort { get; }
    /// <summary>Port for the inbound peer-to-peer listener.</summary>
    int PeerPort { get; }
}

/// <summary>
/// Implements <see cref="IPortConfiguration"/> using well-known default ports. Describes non-config-file
/// behavior; see <see cref="ConfiguredPortConfiguration"/> for how <c>config.json</c> overrides this.
/// Members are <see langword="virtual"/> so a host can inherit and override just one — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultPortConfiguration : IPortConfiguration
{
    /// <inheritdoc />
    public virtual int PeerPort => 50021;
    /// <inheritdoc />
    public virtual int InterfacePort => 50020;
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.PeerPort"/>/<see cref="EngineConfig.InterfacePort"/>
/// over whichever <see cref="IPortConfiguration"/> is registered (Engine default or a host override), when set.
/// Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredPortConfiguration : IPortConfiguration
{
    private readonly IPortConfiguration _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing port overrides.</param>
    public ConfiguredPortConfiguration(IPortConfiguration fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public int PeerPort => _config.PeerPort ?? _fallback.PeerPort;
    /// <inheritdoc />
    public int InterfacePort => _config.InterfacePort ?? _fallback.InterfacePort;
}
