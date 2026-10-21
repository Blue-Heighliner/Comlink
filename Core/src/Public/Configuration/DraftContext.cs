namespace BlueHeighliner.Comlink;

/// <summary>What an <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/> is told about a draft when it is asked whether it is an alert or for its header: the draft as it currently is, which changes as the user edits it.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IDraftContext<TPriority, TLevel, TAspect> : IDraftBlockContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Gets the draft's body text as it currently is.</summary>
    string Body { get; }

    /// <summary>Gets the draft's recipients so far.</summary>
    IReadOnlyList<AddressRequest> Addresses { get; }

    /// <summary>Gets how many monospace characters wide a line of the draft is set to be, or <see langword="null"/> for no limit.</summary>
    int? LineWidth { get; }
}
