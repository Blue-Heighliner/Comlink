namespace BlueHeighliner.Comlink;

/// <summary>
/// A message as the engine uses it: what the user composes and reads, and what the engine stores in the Inbox and the Outbox. The engine has no frame of its own and
/// does not know how a message travels, so a host's frame handler turns a <see cref="Message{TPriority, TLevel, TAspect}"/> into its own frame to send it (see <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}.OnSent"/>)
/// and a received frame into a <see cref="Message{TPriority, TLevel, TAspect}"/> to record it (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.ReceiveMessage"/>).
/// </summary>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public sealed record Message<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
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

    /// <summary>Gets the priority of the message.</summary>
    public required TPriority Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Gets the message level the message is sent at, or <see langword="null"/> for none.</summary>
    public TLevel? MessageLevel { get; init; }

    /// <summary>Gets the message aspect the message carries, or <see langword="null"/> for none.</summary>
    public TAspect? MessageAspect { get; init; }

    /// <summary>Gets whether the message is an alert, which alarms the recipient's Client-mode UI until it is read. The draft handler decides it for a message the user sends (see <see cref="IDraftHandler{TPriority, TLevel, TAspect}.IsAlert"/>) and the host's handler for one it receives.</summary>
    public bool IsAlert { get; init; }
}
