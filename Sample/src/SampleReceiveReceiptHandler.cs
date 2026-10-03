namespace BlueHeighliner.Comlink.Sample;

/// <summary>Treats a <see cref="SampleFrame"/> as a receive receipt when its <see cref="SampleFrame.IsReceiveReceipt"/> flag is set.</summary>
public sealed class SampleReceiveReceiptHandler : IReceiveReceiptHandler<SampleFrame>
{
    /// <inheritdoc />
    public string Priority => SamplePriorities.Receipt;

    /// <inheritdoc />
    public bool IsValid(SampleFrame frame) => frame.IsReceiveReceipt;

    /// <inheritdoc />
    public SampleFrame Create(ReceiptCreateContext context) => new() { IsReceiveReceipt = true, ReceivedMessageId = context.MessageId };

    /// <inheritdoc />
    public string GetMessageId(SampleFrame frame) => frame.ReceivedMessageId;
}
