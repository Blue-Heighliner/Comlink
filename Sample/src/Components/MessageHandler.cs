namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a message when its <see cref="Frame.IsMessage"/> flag is set, mapping the message content onto the frame's own differently named fields.</summary>
public sealed class MessageHandler : IMessageHandler<Frame>
{
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
            Importance = context.Priority,
            Category = context.Tag,
            Classification = context.SecurityLevel
        };

    /// <inheritdoc />
    public DateTime GetSentAt(Frame frame) => frame.Timestamp;

    /// <inheritdoc />
    public string GetBody(Frame frame) => frame.Text;

    /// <inheritdoc />
    public bool GetIsAlert(Frame frame) => frame.Alert;

    /// <inheritdoc />
    public int GetPriority(Frame frame) => frame.Importance;

    /// <inheritdoc />
    public int GetPrintCount(Frame frame) => frame.Alert ? 2 : 1;

    /// <inheritdoc />
    public string GetTag(Frame frame) => frame.Category;

    /// <inheritdoc />
    public string GetSecurityLevel(Frame frame) => frame.Classification;
}
