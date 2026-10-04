namespace BlueHeighliner.Comlink;

/// <summary>The content the engine hands to <see cref="IMessageHandler{TFrame, TPriority, TLevel}.Create"/> to build a message frame.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public sealed record MessageCreateContext<TPriority, TLevel> where TPriority : struct, Enum where TLevel : struct, Enum
{

    /// <summary>Gets the UTC time the message was sent.</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>Gets the body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets whether the message is an alert.</summary>
    public required bool IsAlert { get; init; }

    /// <summary>Gets the message's priority level, which the handler stores as it likes, for example as the member's name.</summary>
    public required TPriority Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the security level the message is sent at, which the handler stores as it likes, or <see langword="null"/> for none (always the case when no security levels are configured).</summary>
    public required TLevel? SecurityLevel { get; init; }
}
