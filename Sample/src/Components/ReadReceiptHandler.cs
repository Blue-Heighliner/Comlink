namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a read receipt when its <see cref="Frame.IsReadReceipt"/> flag is set.</summary>
public sealed class ReadReceiptHandler : IReadReceiptHandler<Frame>
{
    /// <inheritdoc />
    public Enum Priority => MessagePriority.Receipt;

    /// <inheritdoc />
    public bool IsValid(Frame frame) => frame.IsReadReceipt;

    /// <inheritdoc />
    public Frame Create(ReceiptCreateContext context) => new() { IsReadReceipt = true, ReadMessageId = context.MessageId, Recipients = [new Recipient { User = context.To }] };

    /// <inheritdoc />
    public string GetDestination(Frame frame) => frame.Recipients[0].User;

    /// <inheritdoc />
    public string GetSender(Frame frame) => frame.Sender;

    /// <inheritdoc />
    public void SetSender(Frame frame, string sender) => frame.Sender = sender;

    /// <inheritdoc />
    public string GetMessageId(Frame frame) => frame.ReadMessageId;
}
