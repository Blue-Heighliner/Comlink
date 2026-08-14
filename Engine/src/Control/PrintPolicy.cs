namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface for the print manager's automatic "print received" behavior: whether it starts
/// enabled, and how many copies of each received message to add to the print queue while it is.
/// </summary>
public interface IPrintPolicy
{
    /// <summary>
    /// When <see langword="true"/>, the print manager's "print received" toggle (<see cref="ViewModels.IPrintManagerViewModel.PrintReceivedEnabled"/>)
    /// starts enabled, so every received message is automatically added to the print queue from the moment
    /// the app starts. The user can still toggle it off at any time.
    /// </summary>
    bool PrintReceivedDefaultEnabled { get; }
    /// <summary>
    /// Returns how many times <paramref name="message"/> should be automatically added to the print queue
    /// when it arrives — <c>0</c> to not print it, <c>1</c> to print it once, <c>2</c> to print two copies, and
    /// so on. Only consulted while the print manager's "print received" toggle is enabled.
    /// </summary>
    /// <param name="message">The received message, in the host's own <see cref="IMessageFormat.MessageType"/>.</param>
    int GetPrintCount(object message);
}

/// <summary>
/// Implements <see cref="IPrintPolicy"/> disabled by default, printing every received message exactly once
/// once enabled. Describes non-config-file behavior; see <see cref="ConfiguredPrintPolicy"/> for how
/// <c>config.json</c> overrides <see cref="PrintReceivedDefaultEnabled"/>. Members are
/// <see langword="virtual"/> so a host can inherit and override just one — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultPrintPolicy : IPrintPolicy
{
    /// <inheritdoc />
    public virtual bool PrintReceivedDefaultEnabled => false;
    /// <inheritdoc />
    public virtual int GetPrintCount(object message) => 1;
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.PrintReceivedEnabled"/> over whichever
/// <see cref="IPrintPolicy"/> is registered (Engine default or a host override), when set —
/// <see cref="GetPrintCount"/> is left entirely to the wrapped provider, since there is no corresponding
/// <c>config.json</c> field for it. Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>,
/// not by control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredPrintPolicy : IPrintPolicy
{
    private readonly IPrintPolicy _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional print-received-enabled setting.</param>
    public ConfiguredPrintPolicy(IPrintPolicy fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public bool PrintReceivedDefaultEnabled => _config.PrintReceivedEnabled ?? _fallback.PrintReceivedDefaultEnabled;
    /// <inheritdoc />
    public int GetPrintCount(object message) => _fallback.GetPrintCount(message);
}
