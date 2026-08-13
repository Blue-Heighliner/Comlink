namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IAlertConfiguration"/> that renames the alert label to <c>"!ALERT!"</c> — used both by
/// the title bar's alert box and the draft editor's alert checkbox, since both read from this same interface.
/// </summary>
internal sealed class SampleAlertConfiguration : IAlertConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="SampleAlertConfiguration"/> using the given engine configuration for non-label settings.</summary>
    /// <param name="config">Engine configuration providing the alarm duration and quick-confirmation setting.</param>
    public SampleAlertConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public string AlertText => "!ALERT!";
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => TimeSpan.FromSeconds(_config.AlarmSoundSeconds ?? 30);
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => _config.QuickConfirmationEnabled ?? true;
}
