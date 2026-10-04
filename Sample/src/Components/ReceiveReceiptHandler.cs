namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="Frame"/> as a receive receipt when its <see cref="Frame.IsReceiveReceipt"/> flag is set.</summary>
public sealed class ReceiveReceiptHandler : IReceiveReceiptHandler<Frame, MessagePriority>
{
    /// <inheritdoc />
    public MessagePriority Priority => MessagePriority.Receipt;

    /// <inheritdoc />
    public bool IsValid(Frame frame) => frame.IsReceiveReceipt;

    /// <inheritdoc />
    public Frame Create(ReceiptCreateContext context) => new() { IsReceiveReceipt = true, ReceivedMessageId = context.MessageId, Recipients = [new Recipient { User = context.To }] };

    /// <inheritdoc />
    public string GetDestination(Frame frame) => frame.Recipients.FirstOrDefault()?.User ?? string.Empty;

    /// <inheritdoc />
    public string GetSender(Frame frame) => frame.Sender;

    /// <inheritdoc />
    public void SetSender(Frame frame, string sender) => frame.Sender = sender;

    /// <inheritdoc />
    public string GetMessageId(Frame frame) => frame.ReceivedMessageId;
}
