namespace BlueHeighliner.Comlink;

/// <summary>Configures the aspects of one priority level (see <see cref="IPriorityBuilder{TPriority}.Priority"/>); it is also a <see cref="IPriorityBuilder{TPriority}"/>, so the next level can follow straight on.</summary>
/// <typeparam name="TPriority">The enum whose members are the levels.</typeparam>
public interface IPriorityLevelBuilder<TPriority> : IPriorityBuilder<TPriority> where TPriority : struct, Enum
{
    /// <summary>Sets the name of the level, which is also how it is stored in drafts and exports and shown to users. Defaults to the member name in uppercase.</summary>
    /// <param name="label">The name.</param>
    IPriorityLevelBuilder<TPriority> Label(string label);

    /// <summary>Sets whether the GUI offers the level to a user composing a message. It never restricts code. Defaults to <see cref="PriorityMode.User"/>.</summary>
    /// <param name="mode">Whether the GUI offers the level.</param>
    IPriorityLevelBuilder<TPriority> Mode(PriorityMode mode);
}
