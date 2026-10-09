namespace BlueHeighliner.Comlink;

/// <summary>How a custom import format's staged sends (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Import{TFormat}"/>) are sent once the user presses the staged send screen's final send button.</summary>
public enum StagedSendMode
{
    /// <summary>Sends one at a time, in the order added, optionally pausing between each (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Import{TFormat}"/>'s <c>stagedSendDelay</c>). The default.</summary>
    Sequential,
    /// <summary>Sends every staged send at once, without waiting for one to finish before starting the next.</summary>
    Simultaneous
}
