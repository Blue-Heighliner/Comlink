namespace BlueHeighliner.Comlink;

/// <summary>Who may assign a configured priority to a frame. See <see cref="IEngineBuilder.Priorities"/>.</summary>
public enum PriorityMode
{
    /// <summary>A user may choose the priority for a message they compose.</summary>
    User,

    /// <summary>Only the system assigns the priority, through a handler, so it is never offered to a user.</summary>
    System
}
