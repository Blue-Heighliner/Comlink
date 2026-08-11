namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Plays and stops the alarm sound triggered by alert messages (see <see cref="IMessageFormat.GetIsAlert"/>)
/// in Client mode. Actual audio playback is platform-specific, so the engine has no built-in
/// implementation — the default is a silent no-op; register a host implementation to play real audio.
/// </summary>
public interface IAlertSoundPlayer
{
    /// <summary>
    /// Starts playing the alarm sound on a loop. Called whenever a new alert is received while one or
    /// more alerts are already pending; implementations should treat this as idempotent (restarting a
    /// sound that is already looping should not double up playback).
    /// </summary>
    void Play();
    /// <summary>Stops the alarm sound. Called when the auto-stop duration elapses, or when every pending alert has been read.</summary>
    void Stop();
}

/// <summary>Silent no-op default <see cref="IAlertSoundPlayer"/>. Register a host implementation for real audio playback.</summary>
internal sealed class AlertSoundPlayer : IAlertSoundPlayer
{
    /// <inheritdoc />
    public void Play() { }
    /// <inheritdoc />
    public void Stop() { }
}
