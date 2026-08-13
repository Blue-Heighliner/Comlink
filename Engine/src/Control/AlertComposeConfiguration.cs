namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Controls whether the draft compose UI lets a user mark and send a message as an alert
/// (<see cref="IMessageFormat.SetIsAlert"/>). This only affects local origination — disabling it never
/// prevents the app from receiving and alarming on an alert sent by a peer.
/// </summary>
public interface IAlertComposeConfiguration
{
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows the alert checkbox (labeled via
    /// <see cref="IAlertConfiguration.AlertText"/>) so the user can mark and send a draft as an alert.
    /// When <see langword="false"/>, the checkbox is hidden and a draft can never be composed or sent as
    /// an alert from this app — alerts can still arrive from and be raised for a peer-originated message.
    /// </summary>
    bool ComposeAlertsEnabled { get; }
}

/// <summary>Implements <see cref="IAlertComposeConfiguration"/> driven by <see cref="EngineConfig"/>, enabled by default.</summary>
internal sealed class AlertComposeConfiguration : IAlertComposeConfiguration
{
    private readonly EngineConfig _config;

    /// <summary>Initializes a new <see cref="AlertComposeConfiguration"/> reading from the given engine configuration.</summary>
    public AlertComposeConfiguration(EngineConfig config) => _config = config;

    /// <inheritdoc />
    public bool ComposeAlertsEnabled => _config.ComposeAlertsEnabled ?? true;
}
