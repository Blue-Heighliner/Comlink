namespace BlueHeighliner.Comlink;

/// <summary>A message as the engine itself holds it, with the host's priority, message level and message aspect as plain enum members. The host sees the same message as a <see cref="Message{TPriority, TLevel, TAspect}"/>, typed by its own enums.</summary>
internal sealed record Message
{
    /// <summary>Gets the identifier of the message, shared with its peers. The engine gives each message the user sends one.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name of the sender.</summary>
    public required string FromUser { get; init; }

    /// <summary>Gets the body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the addresses the message is for, groups unexpanded.</summary>
    public required IReadOnlyList<MessageAddress> Addresses { get; init; }

    /// <summary>Gets the UTC time the message was sent.</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>Gets the priority of the message, a member of the enum the host stated for its priorities.</summary>
    public required Enum Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Gets the message level the message is sent at, a member of the enum the host stated for its message levels, or <see langword="null"/> for none.</summary>
    public Enum? MessageLevel { get; init; }

    /// <summary>Gets the message aspect the message carries, a member of the enum the host stated for its message aspects, or <see langword="null"/> for none.</summary>
    public Enum? MessageAspect { get; init; }

    /// <summary>Gets whether the message is an alert, which alarms the recipient's Client-mode UI until it is read. The draft handler decides it for a message the user sends (see <see cref="IDraftHandler{TPriority, TLevel, TAspect}.IsAlert"/>) and the host's processor for one it receives.</summary>
    public bool IsAlert { get; init; }
}
