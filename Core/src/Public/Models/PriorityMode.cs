namespace BlueHeighliner.Comlink;

/// <summary>Whether a configured priority can be chosen by a user in the GUI. It never restricts code, which may use any configured priority. See <see cref="IEngineBuilder.Priorities{TPriority}"/>.</summary>
public enum PriorityMode
{
    /// <summary>The GUI offers the priority to a user composing a message.</summary>
    User,

    /// <summary>The GUI never offers the priority to a user; it is for code to use, such as the priority a retrieval or receipt handler names.</summary>
    System
}
