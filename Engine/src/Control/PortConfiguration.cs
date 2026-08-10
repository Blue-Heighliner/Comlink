namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Provides TCP port numbers for the peer-to-peer and interface listeners.</summary>
public interface IPortConfiguration
{
    /// <summary>Port for the inbound interface listener. Always active, regardless of mode.</summary>
    int InterfacePort { get; }
    /// <summary>Port for the inbound peer-to-peer listener.</summary>
    int PeerPort { get; }
}

/// <summary>Implements <see cref="IPortConfiguration"/> using port values from engine configuration, falling back to well-known defaults.</summary>
internal sealed class PortConfiguration : IPortConfiguration
{
    /// <summary>Initializes a new instance applying configured port values, falling back to defaults when <see langword="null"/>.</summary>
    /// <param name="config">Engine configuration providing port overrides.</param>
    public PortConfiguration(EngineConfig config)
    {
        PeerPort = config.PeerPort ?? 50021;
        InterfacePort = config.InterfacePort ?? 50020;
    }

    /// <inheritdoc />
    public int PeerPort { get; }
    /// <inheritdoc />
    public int InterfacePort { get; }
}
