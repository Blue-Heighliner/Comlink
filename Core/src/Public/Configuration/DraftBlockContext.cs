namespace BlueHeighliner.Comlink;

/// <summary>What an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is asked about when the engine checks whether a combination may be entered: the choices a draft would have.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IDraftBlockContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the draft's tag, or an empty string for none, as the user typed it after the tag rules have been applied.</summary>
    string Tag { get; }

    /// <summary>Gets the priority level the draft would be sent at.</summary>
    TPriority Priority { get; }

    /// <summary>Gets the message level the draft would be sent at, or <see langword="null"/> when no message levels are configured.</summary>
    TLevel? MessageLevel { get; }

    /// <summary>Gets the message aspect the draft would carry, or <see langword="null"/> for none.</summary>
    TAspect? MessageAspect { get; }
}
