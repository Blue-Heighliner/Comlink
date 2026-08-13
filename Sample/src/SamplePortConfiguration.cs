namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IPortConfiguration"/> that reads ports from config, falling back to
/// <c>PEER_LISTEN_PORT</c>/<c>INTERFACE_LISTEN_PORT</c> environment variables, then the same
/// well-known defaults as the Engine default.
/// </summary>
public sealed class SamplePortConfiguration : IPortConfiguration
{
    /// <summary>Initializes a new <see cref="SamplePortConfiguration"/> applying configured, then environment, then default port values.</summary>
    /// <param name="config">Engine configuration providing port overrides.</param>
    public SamplePortConfiguration(EngineConfig config)
    {
        PeerPort = config.PeerPort ?? ReadEnvPort("PEER_LISTEN_PORT") ?? 50021;
        InterfacePort = config.InterfacePort ?? ReadEnvPort("INTERFACE_LISTEN_PORT") ?? 50020;
    }

    /// <inheritdoc />
    public int PeerPort { get; }
    /// <inheritdoc />
    public int InterfacePort { get; }

    private static int? ReadEnvPort(string variable) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out int port) ? port : null;
}
