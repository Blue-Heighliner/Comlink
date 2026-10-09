namespace BlueHeighliner.Comlink;

/// <summary>Whether a configured priority can be chosen by a user in the GUI. It never restricts code, which may use any configured priority. See <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Priority"/>.</summary>
public enum PriorityMode
{
    /// <summary>The GUI offers the priority to a user composing a message.</summary>
    User,

    /// <summary>The GUI never offers the priority to a user; it is for code to use, such as the priority a processor sends a receipt or a retrieval request with.</summary>
    System
}
