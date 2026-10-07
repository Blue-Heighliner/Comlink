namespace BlueHeighliner.Comlink;

/// <summary>Controls the alarm a Client-mode instance raises when an alert message is received. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Alarms{THandler}"/>. Every member is optional.</summary>
public interface IAlarmHandler
{
    /// <summary>Gets how long the alarm sound plays after an alert is received before it stops by itself; a further alert received while it is sounding starts the time again. Defaults to 30 seconds.</summary>
    TimeSpan AlertDuration => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets how long the alarm sound plays after a connection drops, in every role. It is separate from the alert alarm. A further connection dropping while it is sounding starts the time again, and it stops early
    /// once every connection that dropped during the current alarm is back up. Defaults to 30 seconds.
    /// </summary>
    TimeSpan DisconnectDuration => TimeSpan.FromSeconds(30);
}
