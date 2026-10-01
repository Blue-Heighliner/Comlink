namespace BlueHeighliner.Comlink;

/// <summary>The content the engine hands to <see cref="IMessageHandler{TFrame}.Create"/> to build a message frame.</summary>
public sealed record MessageCreateContext
{

    /// <summary>Gets the UTC time the message was sent.</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>Gets the body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets whether the message is an alert.</summary>
    public required bool IsAlert { get; init; }

    /// <summary>Gets the priority number, one of the engine's configured priorities.</summary>
    public required int Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the security level name the message is sent at, or an empty string when none are configured.</summary>
    public required string SecurityLevel { get; init; }
}
