namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a receive receipt when its <see cref="Frame.IsReceiveReceipt"/> flag is set.</summary>
public sealed class ReceiveReceiptHandler : IReceiveReceiptHandler<Frame>
{
    /// <inheritdoc />
    public Enum Priority => MessagePriority.Receipt;

    /// <inheritdoc />
    public bool IsValid(Frame frame) => frame.IsReceiveReceipt;

    /// <inheritdoc />
    public Frame Create(ReceiptCreateContext context) => new() { IsReceiveReceipt = true, ReceivedMessageId = context.MessageId, Recipients = [new Recipient { User = context.To }] };

    /// <inheritdoc />
    public string GetDestination(Frame frame) => frame.Recipients[0].User;

    /// <inheritdoc />
    public string GetSender(Frame frame) => frame.Sender;

    /// <inheritdoc />
    public void SetSender(Frame frame, string sender) => frame.Sender = sender;

    /// <inheritdoc />
    public string GetMessageId(Frame frame) => frame.ReceivedMessageId;
}
