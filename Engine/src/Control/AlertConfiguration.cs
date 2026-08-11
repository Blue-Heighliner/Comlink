namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Configures the alarm behavior triggered by alert messages (see <see cref="IMessageFormat.GetIsAlert"/>)
/// in Client mode: the text shown in the title bar's alert box, how long the alarm sound plays before
/// automatically stopping, and whether quick confirmation (click or Space/Enter) is enabled. See
/// <c>Docs/ViewModels.md</c>.
/// </summary>
public interface IAlertConfiguration
{
    /// <summary>Text shown in the title bar's alert box while alarming.</summary>
    string AlertText { get; }
    /// <summary>
    /// How long the alarm sound plays after an alert is received before <see cref="IAlertSoundPlayer.Stop"/>
    /// is called automatically. Resets (restarts from this full duration) whenever a new alert is received
    /// while already alarming. Does not affect the alert box itself, which stays visible until every
    /// pending alert has been read.
    /// </summary>
    TimeSpan AlarmSoundDuration { get; }
    /// <summary>
    /// When <see langword="true"/>, clicking the alert box, or pressing Space/Enter while focus is not in
    /// a text input, confirms (marks read) the latest unconfirmed alert. Repeating the action confirms
    /// pending alerts one at a time, most-recently-received first.
    /// </summary>
    bool QuickConfirmationEnabled { get; }
}

/// <summary>Implements <see cref="IAlertConfiguration"/> driven by <see cref="EngineConfig"/>, with sensible defaults.</summary>
internal sealed class AlertConfiguration : IAlertConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="AlertConfiguration"/> reading from the given engine configuration.</summary>
    public AlertConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string AlertText => string.IsNullOrEmpty(_config.AlertText) ? "ALERT" : _config.AlertText;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => TimeSpan.FromSeconds(_config.AlarmSoundSeconds ?? 30);
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => _config.QuickConfirmationEnabled ?? true;
}
