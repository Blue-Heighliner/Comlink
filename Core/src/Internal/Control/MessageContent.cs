namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped content of a message to create, which a <see cref="IMessageFrameHandler"/> turns into the host's typed <see cref="MessageCreateContext{TPriority, TLevel}"/>.</summary>
internal sealed record MessageContent
{
    /// <summary>Gets the UTC time the message was sent.</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>Gets the body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the message's priority level, a member of the host's priority enum.</summary>
    public required Enum Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the security level name the message is sent at, or an empty string when none are configured.</summary>
    public required string SecurityLevel { get; init; }
}
