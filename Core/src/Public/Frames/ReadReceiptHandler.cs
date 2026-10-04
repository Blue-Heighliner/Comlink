namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are read receipts: sent back to the sender of a message when its recipient opens it, so the
/// sender's delivery status for that recipient advances to read. A receipt is not a message. See <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.ReadReceipt{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
public interface IReadReceiptHandler<TFrame, TPriority> where TFrame : class where TPriority : struct, Enum
{
    /// <summary>Gets the priority level that read receipts are sent with, which is how they are ordered against other traffic.</summary>
    TPriority Priority { get; }

    /// <summary>Returns whether <paramref name="frame"/> is a read receipt.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new read receipt frame for <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. The engine sets the identifier, sender, addresses and sent time itself.</summary>
    /// <param name="context">The message the receipt is for.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(ReceiptCreateContext context);

    /// <summary>Gets the user name of the sender of <paramref name="frame"/>.</summary>
    string GetSender(TFrame frame);

    /// <summary>Sets the user name of the sender of <paramref name="frame"/>, which the engine does for every read receipt it sends.</summary>
    /// <param name="frame">The read receipt.</param>
    /// <param name="sender">The sender's user name.</param>
    void SetSender(TFrame frame, string sender);

    /// <summary>Gets the user name <paramref name="frame"/> is for: where the engine sends this read receipt, and where a server or relay hands it on to. The engine states it when it creates the frame (see <see cref="ReceiptCreateContext.To"/>).</summary>
    string GetDestination(TFrame frame);

    /// <summary>Gets the identifier of the message <paramref name="frame"/> is a read receipt for.</summary>
    string GetMessageId(TFrame frame);
}
