namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a message when its <see cref="Frame.IsMessage"/> flag is set, mapping the message content onto the frame's own differently named fields. It also numbers messages in sequence, continuing from the identifier the engine kept from the previous run: an identifier is a token chosen at random for each run, so nodes practically never collide, a dash and a counter that counts on from the previous identifier's, so it never starts over after a restart.</summary>
public sealed class MessageHandler : IMessageHandler<Frame>
{
    private readonly string run = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();

    /// <inheritdoc />
    public bool IsValid(Frame frame) => frame.IsMessage;

    /// <inheritdoc />
    public Frame Create(MessageCreateContext context)
        => new()
        {
            IsMessage = true,
            Timestamp = context.SentAt,
            Text = context.Body,
            Alert = context.IsAlert,
            Importance = context.Priority.ToString(),
            Category = context.Tag,
            Classification = context.SecurityLevel
        };

    /// <inheritdoc />
    public string GetId(Frame frame) => frame.Id;

    /// <inheritdoc />
    public void SetId(Frame frame, string id) => frame.Id = id;

    /// <inheritdoc />
    public string NextId(string? previous)
    {
        long count = previous is not null && previous.LastIndexOf('-') is > 0 and int dash && long.TryParse(previous[(dash + 1)..], out long last) ? last + 1 : 1;
        return $"{run}-{count:D8}";
    }

    /// <inheritdoc />
    public IEnumerable<(string Name, AddressType Type, string Information)> GetAddresses(Frame frame)
        => frame.Recipients.Select(recipient => (recipient.User, recipient.Kind switch { "CC" => AddressType.Cc, "OUTSIDE" => AddressType.External, _ => AddressType.To }, recipient.Note));

    /// <inheritdoc />
    public void SetAddresses(Frame frame, IReadOnlyList<(string Name, AddressType Type, string Information)> addresses)
        => frame.Recipients = [.. addresses.Select(address => new Recipient { User = address.Name, Kind = address.Type switch { AddressType.Cc => "CC", AddressType.External => "OUTSIDE", _ => "TO" }, Note = address.Information })];

    /// <inheritdoc />
    public string GetSender(Frame frame) => frame.Sender;

    /// <inheritdoc />
    public void SetSender(Frame frame, string sender) => frame.Sender = sender;

    /// <inheritdoc />
    public DateTime GetSentAt(Frame frame) => frame.Timestamp;

    /// <inheritdoc />
    public string GetBody(Frame frame) => frame.Text;

    /// <inheritdoc />
    public bool GetIsAlert(Frame frame) => frame.Alert;

    /// <inheritdoc />
    public Enum GetPriority(Frame frame) => Enum.TryParse(frame.Importance, ignoreCase: true, out MessagePriority priority) ? priority : MessagePriority.Low;

    /// <inheritdoc />
    public int GetPrintCount(Frame frame) => frame.Alert ? 2 : 1;

    /// <inheritdoc />
    public string GetTag(Frame frame) => frame.Category;

    /// <inheritdoc />
    public string GetSecurityLevel(Frame frame) => frame.Classification;
}
