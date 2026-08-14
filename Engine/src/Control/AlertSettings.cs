namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface configuring the alert-message feature (see <see cref="IMessageFormat.GetIsAlert"/>) in
/// Client mode: the alarm's title-bar text and checkbox label, how long its sound plays before
/// automatically stopping, whether quick confirmation is enabled, and whether the draft editor can
/// originate alerts at all. Actually playing the alarm sound is real platform behavior, not configuration
/// — see <see cref="IAlertSoundPlayer"/> instead. See <c>Docs/ViewModels.md</c>.
/// </summary>
public interface IAlertSettings
{
    /// <summary>Text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label.</summary>
    string AlertText { get; }
    /// <summary>
    /// How long the alarm sound plays after an alert is received before it is automatically stopped
    /// (see <see cref="IAlertSoundPlayer.Stop"/>). Resets (restarts from this full duration) whenever
    /// a new alert is received while already alarming. Does not affect the alert box itself, which stays
    /// visible until every pending alert has been read.
    /// </summary>
    TimeSpan AlarmSoundDuration { get; }
    /// <summary>
    /// When <see langword="true"/>, clicking the alert box, or pressing Space/Enter while focus is not in
    /// a text input, confirms (marks read) the latest unconfirmed alert. Repeating the action confirms
    /// pending alerts one at a time, most-recently-received first.
    /// </summary>
    bool QuickConfirmationEnabled { get; }
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows the alert checkbox so the user can mark and send
    /// a draft as an alert. When <see langword="false"/>, the checkbox is hidden and a draft can never be
    /// composed or sent as an alert from this app — alerts can still arrive from and be raised for a
    /// peer-originated message.
    /// </summary>
    bool ComposeAlertsEnabled { get; }
}

/// <summary>
/// Implements <see cref="IAlertSettings"/> with sensible hardcoded defaults. Describes non-config-file
/// behavior; see <see cref="ConfiguredAlertSettings"/> for how <c>config.json</c> overrides this. Members
/// are <see langword="virtual"/> so a host can inherit and override just one — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultAlertSettings : IAlertSettings
{
    /// <inheritdoc />
    public virtual string AlertText => "ALERT";
    /// <inheritdoc />
    public virtual TimeSpan AlarmSoundDuration => TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual bool QuickConfirmationEnabled => true;
    /// <inheritdoc />
    public virtual bool ComposeAlertsEnabled => true;
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.AlertText"/>/<see cref="EngineConfig.AlarmSoundSeconds"/>/
/// <see cref="EngineConfig.QuickConfirmationEnabled"/>/<see cref="EngineConfig.ComposeAlertsEnabled"/> over
/// whichever <see cref="IAlertSettings"/> is registered (Engine default or a host override), when set.
/// Registered by <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface
/// convention scanning.
/// </summary>
internal sealed class ConfiguredAlertSettings : IAlertSettings
{
    private readonly IAlertSettings _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional overrides.</param>
    public ConfiguredAlertSettings(IAlertSettings fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public string AlertText => string.IsNullOrEmpty(_config.AlertText) ? _fallback.AlertText : _config.AlertText;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => _config.AlarmSoundSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : _fallback.AlarmSoundDuration;
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => _config.QuickConfirmationEnabled ?? _fallback.QuickConfirmationEnabled;
    /// <inheritdoc />
    public bool ComposeAlertsEnabled => _config.ComposeAlertsEnabled ?? _fallback.ComposeAlertsEnabled;
}
