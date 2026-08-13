namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Controls the starting state of the print manager's "print received" toggle.</summary>
public interface IPrintReceivedDefaultProvider
{
    /// <summary>
    /// When <see langword="true"/>, the print manager's "print received" toggle starts enabled, so every
    /// received message is automatically added to the print queue (subject to <see cref="IPrintReceivedRule"/>)
    /// from the moment the app starts. The user can still toggle it off at any time in the print manager.
    /// </summary>
    bool DefaultEnabled { get; }
}

/// <summary>Implements <see cref="IPrintReceivedDefaultProvider"/> driven by <see cref="EngineConfig"/>, disabled by default.</summary>
internal sealed class PrintReceivedDefaultProvider : IPrintReceivedDefaultProvider
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="PrintReceivedDefaultProvider"/> reading from the given engine configuration.</summary>
    public PrintReceivedDefaultProvider(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool DefaultEnabled => _config.PrintReceivedEnabled ?? false;
}
