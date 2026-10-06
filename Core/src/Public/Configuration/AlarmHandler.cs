namespace BlueHeighliner.Comlink;

/// <summary>Controls the alarm a Client-mode instance raises when an alert message is received. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.Alarms{THandler}"/>. Every member is optional.</summary>
public interface IAlarmHandler
{
    /// <summary>Gets how long the alarm sound plays after an alert is received before it stops by itself; a further alert received while it is sounding starts the time again. Defaults to 30 seconds. The current user's entry in the network configuration file can override it.</summary>
    TimeSpan AlertDuration => TimeSpan.FromSeconds(30);
}
