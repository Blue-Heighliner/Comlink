namespace BlueHeighliner.Comlink;

/// <summary>The input the engine hands to <see cref="IReadReceiptHandler{TFrame, TPriority}.Create"/> and <see cref="IReceiveReceiptHandler{TFrame, TPriority}.Create"/> to build a receipt frame.</summary>
public sealed record ReceiptCreateContext
{
    /// <summary>Gets the identifier of the message the receipt is for.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets the user the receipt is for, the sender of the message, which the handler stores as the receipt's destination.</summary>
    public required string To { get; init; }
}
