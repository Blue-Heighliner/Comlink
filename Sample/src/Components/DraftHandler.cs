namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// A draft is shown 60 characters wide by default (the user may change it between 10 and 90), and carries a header that depends on what kind of message it is, which is shown above the body, can not be edited,
/// follows the draft as its aspects change, and a message is an alert when its tag is <c>ALERT</c>, is numbered in sequence continuing from the identifier the engine kept from the previous run (a token chosen at random for each run, so nodes practically never collide, a dash and a counter that counts on from the previous identifier's, so it never starts over after a restart), and needs a tag, which starts as <c>NOTICE</c> (a new draft also starts at medium importance and the internal message level), is forced to uppercase, at most 12 characters (the tag box is exactly that wide), and may hold letters and numbers but no symbols or spaces, and is put in front of the message when it is sent:
/// an alert has <c>ALERT - ACTION REQUIRED</c>, otherwise a message tagged <c>URGENT</c> has <c>URGENT - PLEASE REPLY</c> and one tagged <c>REPORT</c> has <c>REPORT - FOR YOUR REVIEW</c>, and a message at the
/// restricted message level has a second line, <c>RESTRICTED - DO NOT FORWARD</c>, whatever else it is. Every other message, such as a plain one or one tagged <c>NOTICE</c>, has no header at all.
/// </summary>
public sealed class DraftHandler : IDraftHandler<MessagePriority, MessageLevel, MessageAspect>
{
    private readonly string run = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();

    /// <inheritdoc />
    public int? DefaultLineWidth => 60;

    /// <inheritdoc />
    public int MinLineWidth => 10;

    /// <inheritdoc />
    public int? MaxLineWidth => 90;

    /// <inheritdoc />
    public bool IsTagRequired => true;

    /// <inheritdoc />
    public string? DefaultTag => "NOTICE";

    /// <inheritdoc />
    public MessagePriority? DefaultPriority => MessagePriority.Medium;

    /// <inheritdoc />
    public MessageLevel? DefaultMessageLevel => MessageLevel.Internal;

    /// <inheritdoc />
    public TagCase TagCase => TagCase.Upper;

    /// <inheritdoc />
    public int? MaxTagLength => 12;

    /// <inheritdoc />
    public bool AllowTagSymbols => false;

    /// <inheritdoc />
    public bool AllowTagSpaces => false;

    /// <inheritdoc />
    public bool IsAlert(DraftState<MessagePriority, MessageLevel, MessageAspect> state) => state.Tag is "ALERT";

    /// <inheritdoc />
    public string NextId(string? previous)
    {
        long count = previous is not null && previous.LastIndexOf('-') is > 0 and int dash && long.TryParse(previous[(dash + 1)..], out long last) ? last + 1 : 1;
        return $"{run}-{count:D8}";
    }

    /// <inheritdoc />
    public string? GetHeader(DraftState<MessagePriority, MessageLevel, MessageAspect> state)
    {
        List<string> lines = [];
        if (IsAlert(state))
        {
            lines.Add("ALERT - ACTION REQUIRED");
        }
        else if (state.Tag is "URGENT")
        {
            lines.Add("URGENT - PLEASE REPLY");
        }
        else if (state.Tag is "REPORT")
        {
            lines.Add("REPORT - FOR YOUR REVIEW");
        }

        if (state.MessageLevel is MessageLevel.Restricted)
        {
            lines.Add("RESTRICTED - DO NOT FORWARD");
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }
}
