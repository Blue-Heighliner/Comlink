namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IAlertSettings"/> that renames the alert label to <c>"!ALERT!"</c> — used both by the
/// title bar's alert box and the draft editor's alert checkbox, since both read from this same interface.
/// Alarm duration and quick-confirmation are left at the same hardcoded defaults as the Engine default,
/// since this override isn't meant to change them — <c>config.json</c> can still override either, applied
/// separately at the Engine level (see <c>Docs/Control.md</c>). Actual alarm sound playback is real
/// platform behavior provided by the engine itself, not something Sample overrides here.
/// </summary>
public sealed class SampleAlertSettings : DefaultAlertSettings
{
    /// <inheritdoc />
    public override string AlertText => "!ALERT!";
}
