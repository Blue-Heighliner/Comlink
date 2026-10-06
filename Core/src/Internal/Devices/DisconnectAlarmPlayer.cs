namespace BlueHeighliner.Comlink;

/// <summary>
/// Plays and stops the alarm sound for dropped connections (see <see cref="IDisconnectAlarmService"/>), on a player of its own so it never
/// starts or stops the sound of the alert alarm (see <see cref="IAlertSoundPlayer"/>), nor the other way round. Real OS-level audio playback, not configuration.
/// </summary>
internal interface IDisconnectAlarmPlayer
{
    /// <summary>Starts playing the alarm sound on a loop. Idempotent.</summary>
    void Play();
    /// <summary>Stops the alarm sound.</summary>
    void Stop();
}

/// <inheritdoc cref="IDisconnectAlarmPlayer" />
[ExcludeFromCodeCoverage]
internal sealed class DisconnectAlarmPlayer : IDisconnectAlarmPlayer
{
    private readonly AlertSoundPlayer player = new();

    /// <inheritdoc />
    public void Play() => player.Play();

    /// <inheritdoc />
    public void Stop() => player.Stop();
}
