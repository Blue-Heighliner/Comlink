namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are read receipts: sent back to the sender of a message when its recipient opens it, so the
/// sender's delivery status for that recipient advances to read. A receipt is not a message. See <see cref="IFrameBuilder{TFrame}.ReadReceipt{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IReadReceiptHandler<TFrame> where TFrame : class
{
    /// <summary>Gets the member of the priority enum (see <see cref="IEngineBuilder.Priorities{TPriority}"/>) naming the level that read receipts are sent with, which is how they are ordered against other traffic.</summary>
    Enum Priority { get; }

    /// <summary>Returns whether <paramref name="frame"/> is a read receipt.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new read receipt frame for <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender, addresses and sent time itself.</summary>
    /// <param name="context">The message the receipt is for.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(ReceiptCreateContext context);

    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a read receipt for.</summary>
    string GetMessageId(TFrame frame);
}
