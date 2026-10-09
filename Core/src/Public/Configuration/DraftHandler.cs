namespace BlueHeighliner.Comlink;

/// <summary>
/// Controls how drafts are composed: how wide a line may be, a header every message must start with, and which combinations of priority, level, aspect and tag are allowed. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Drafts{THandler}"/>. Every member is optional.
/// </summary>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IDraftHandler<TPriority, TLevel, TAspect> where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>
    /// Gets how many monospace characters wide a line of a new draft is shown, which the user can change in the draft view between <see cref="MinLineWidth"/> and <see cref="MaxLineWidth"/>. <see langword="null"/> (the default) is no limit,
    /// or <see cref="MaxLineWidth"/> when that is stated, since no limit would exceed it. It only changes how the draft is shown, never its text. The draft view only offers the width when this or <see cref="MaxLineWidth"/> is stated.
    /// </summary>
    int? DefaultLineWidth => null;

    /// <summary>Gets the narrowest a line may be, in monospace characters. Defaults to <c>1</c>. A line is never narrower than the longest line of the draft's header, whatever this says, and a header wider than <see cref="MaxLineWidth"/> wins over it.</summary>
    int MinLineWidth => 1;

    /// <summary>Gets the widest a line may be, in monospace characters, or <see langword="null"/> (the default) for no maximum, in which case the user may also clear the width to have no limit.</summary>
    int? MaxLineWidth => null;

    /// <summary>Gets a value indicating whether messages carry a tag, shown in the draft editor, the message view and the entry list. Defaults to <see langword="true"/>.</summary>
    bool EnableTags => true;

    /// <summary>Gets which letter case message tags are kept in: <see cref="Comlink.TagCase.Mixed"/> (the default) leaves them as written, the others force them, as they are typed, to lowercase or uppercase.</summary>
    TagCase TagCase => TagCase.Mixed;

    /// <summary>Gets whether a message must have a tag: a draft without one can not be sent. Defaults to <see langword="false"/>.</summary>
    bool IsTagRequired => false;

    /// <summary>
    /// Says whether a draft may have this combination of priority, message level, message aspect and tag. The draft view asks it for every choice the user could make and offers only the allowed ones, and a draft that is
    /// already a blocked combination cannot be sent, so a blocked combination is never composed. Defaults to <see langword="true"/>: every combination is allowed.
    /// </summary>
    /// <param name="context">A snapshot of the engine, such as the current user and the users who are connected.</param>
    /// <param name="priority">The priority the draft would be sent at.</param>
    /// <param name="level">The message level the draft would be sent at, or <see langword="null"/> when no message levels are configured.</param>
    /// <param name="aspect">The message aspect the draft would carry, or <see langword="null"/> for none.</param>
    /// <param name="tag">The tag the draft would have, empty for none, as the user typed it after the tag rules have been applied.</param>
    /// <returns><see langword="true"/> if the combination is allowed, <see langword="false"/> if it is blocked.</returns>
    bool IsAllowed(IEngineContext context, TPriority priority, TLevel? level, TAspect? aspect, string tag) => true;

    /// <summary>
    /// Says whether a message sent from the draft in <paramref name="state"/> is an alert, which alarms the recipient's Client-mode UI until it is read. Asked as the user edits the draft, to show the alert mark, and when it is sent, to
    /// set <see cref="Message.IsAlert"/>. Defaults to <see langword="false"/>: nothing is an alert unless the host says so.
    /// </summary>
    /// <param name="state">The draft as it currently is.</param>
    bool IsAlert(DraftState<TPriority, TLevel, TAspect> state) => false;

    /// <summary>
    /// Gets the identifier of the next message the user sends, given <paramref name="previous"/>, the one the engine generated last, which it keeps between runs (<see langword="null"/> before the first). An identifier is shared
    /// with the peers, who tell messages apart by it, so it is unique among every message in the network. Defaults to a new GUID without hyphens, which ignores <paramref name="previous"/>.
    /// </summary>
    /// <param name="previous">The identifier generated last, or <see langword="null"/> when there is none.</param>
    string NextId(string? previous) => Guid.NewGuid().ToString("N");

    /// <summary>Gets the tag a new draft starts with, or <see langword="null"/> (the default) for none. It is made to fit the tag rules, as anything typed is.</summary>
    string? DefaultTag => null;

    /// <summary>Gets the priority level a new draft starts at, or <see langword="null"/> (the default) for the lowest level the user may choose.</summary>
    TPriority? DefaultPriority => null;

    /// <summary>Gets the message level a new draft starts at, or <see langword="null"/> (the default) for the highest level the user may use. A level above what the user may use is brought down to it.</summary>
    TLevel? DefaultMessageLevel => null;

    /// <summary>Gets the message aspect a new draft starts with, or <see langword="null"/> (the default) for none.</summary>
    TAspect? DefaultMessageAspect => null;

    /// <summary>Gets the fewest characters a message tag may have once it is given. Defaults to <c>0</c>. Use <see cref="IsTagRequired"/> to require a tag at all.</summary>
    int MinTagLength => 0;

    /// <summary>Gets the most characters a message tag may have, or <see langword="null"/> (the default) for no maximum. The draft view sizes its tag box to fit exactly that many monospace characters, and does not let more be typed.</summary>
    int? MaxTagLength => null;

    /// <summary>Gets whether a message tag may contain symbols and punctuation. Defaults to <see langword="true"/>.</summary>
    bool AllowTagSymbols => true;

    /// <summary>Gets whether a message tag may contain digits. Defaults to <see langword="true"/>.</summary>
    bool AllowTagNumbers => true;

    /// <summary>Gets whether a message tag may contain spaces. Defaults to <see langword="true"/>.</summary>
    bool AllowTagSpaces => true;

    /// <summary>
    /// Returns the header every message sent from the draft in <paramref name="state"/> must start with, or <see langword="null"/> (the default) for none. The engine asks again whenever one of the draft's aspects
    /// (its tag, priority, message level, alert flag, recipients or line width) changes. The draft view shows a header above the body, where the user cannot edit it, and it is put in front of the body, followed by a line break, when the draft is sent.
    /// It is shown wrapped to the line width like the body, but only shown so: no line break is added to it or to the body.
    /// </summary>
    /// <param name="state">The draft as it currently is.</param>
    string? GetHeader(DraftState<TPriority, TLevel, TAspect> state) => null;
}
