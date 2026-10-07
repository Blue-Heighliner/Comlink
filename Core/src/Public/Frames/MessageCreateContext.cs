namespace BlueHeighliner.Comlink;

/// <summary>The content the engine hands to <see cref="IMessageHandler{TFrame, TPriority, TLevel, TAspect}.Create"/> to build a message frame.</summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public sealed record MessageCreateContext<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{

    /// <summary>Gets the UTC time the message was sent.</summary>
    public required DateTime SentAt { get; init; }

    /// <summary>Gets the body text.</summary>
    public required string Body { get; init; }

    /// <summary>Gets the message's priority level, which the handler stores as it likes, for example as the member's name.</summary>
    public required TPriority Priority { get; init; }

    /// <summary>Gets the short tag identifying the type of message, or an empty string for none.</summary>
    public required string Tag { get; init; }

    /// <summary>Gets the message level the message is sent at, which the handler stores as it likes, or <see langword="null"/> for none (always the case when no message levels are configured).</summary>
    public required TLevel? MessageLevel { get; init; }

    /// <summary>Gets the message aspect the message carries, which the handler stores as it likes, or <see langword="null"/> for none.</summary>
    public TAspect? MessageAspect { get; init; }
}
