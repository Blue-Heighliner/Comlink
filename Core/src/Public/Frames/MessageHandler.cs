namespace BlueHeighliner.Comlink;

/// <summary>
/// Handles the frames of the host's frame type <typeparamref name="TFrame"/> that are messages: the ones the user reads, which are stored in the Inbox when received
/// and in the Outbox when sent. A frame that is not a message is still routed and handed to the network processor, but never shown or stored. See <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Message{THandler}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the security levels.</typeparam>
public interface IMessageHandler<TFrame, TPriority, TLevel> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum
{
    /// <summary>Returns whether <paramref name="frame"/> is a message.</summary>
    /// <param name="frame">The frame to classify.</param>
    bool IsValid(TFrame frame);

    /// <summary>Creates a new message frame carrying <paramref name="context"/>, for which <see cref="IsValid"/> returns <see langword="true"/>. A new message has no identifier yet (<see cref="GetId"/> returns an empty string), and the engine sets the identifier, sender and addresses itself.</summary>
    /// <param name="context">The content of the message.</param>
    /// <returns>The new frame.</returns>
    TFrame Create(MessageCreateContext<TPriority, TLevel> context);

    /// <summary>Gets the identifier of <paramref name="frame"/>, or an empty string while it is unset, which is how a newly created message starts: a message sent while its identifier is unset is given a generated one (see <see cref="NextId"/>). Only messages have an identifier: how receipts, retrievals and stored copies refer to a message, so it must be unique across the whole network. A receipt is identified by the identifier of the message it is for.</summary>
    string GetId(TFrame frame);

    /// <summary>Sets the identifier of <paramref name="frame"/>.</summary>
    /// <param name="frame">The message to give the identifier to.</param>
    /// <param name="id">The identifier.</param>
    void SetId(TFrame frame, string id);

    /// <summary>
    /// Generates the identifier of the next message this node creates. The engine keeps the last one it generated in the user's database between restarts and hands it back here, so a sequence can continue.
    /// The default is a random GUID in 32 uppercase hexadecimal characters.
    /// </summary>
    /// <param name="previous">The identifier generated last, or <see langword="null"/> when none has been generated yet.</param>
    string NextId(string? previous) => Guid.NewGuid().ToString("N").ToUpperInvariant();

    /// <summary>Gets the user name of the sender of <paramref name="frame"/>.</summary>
    string GetSender(TFrame frame);

    /// <summary>Sets the user name of the sender of <paramref name="frame"/>, which the engine does for every message it sends.</summary>
    /// <param name="frame">The message.</param>
    /// <param name="sender">The sender's user name.</param>
    void SetSender(TFrame frame, string sender);

    /// <summary>
    /// Gets the recipient list of <paramref name="frame"/>, converting from the host's own recipient shape: a name, whether it is addressed to (<see cref="AddressType.To"/>), copied (<see cref="AddressType.Cc"/>) or outside the
    /// system (<see cref="AddressType.External"/>, information for the user that the engine takes no action for), and any custom instructions attached to it (for example <c>Deliver to Eastside Office</c>, an empty string when there are none).
    /// </summary>
    IEnumerable<(string Name, AddressType Type, string Information)> GetAddresses(TFrame frame);

    /// <summary>Sets the recipient list of <paramref name="frame"/>, converting to the host's own recipient shape, when the engine builds a message.</summary>
    /// <param name="frame">The message.</param>
    /// <param name="addresses">The recipients.</param>
    void SetAddresses(TFrame frame, IReadOnlyList<(string Name, AddressType Type, string Information)> addresses);

    /// <summary>Gets the UTC time <paramref name="frame"/> was sent.</summary>
    DateTime GetSentAt(TFrame frame);

    /// <summary>Gets the body text of <paramref name="frame"/>.</summary>
    string GetBody(TFrame frame);

    /// <summary>Gets whether <paramref name="frame"/> is an alert, a message that makes a receiving Client-mode UI alarm until the user reads it.</summary>
    bool GetIsAlert(TFrame frame);

    /// <summary>Gets the priority level <paramref name="frame"/> was created with (see <see cref="MessageCreateContext{TPriority, TLevel}.Priority"/>), which also sets the send priority. A message received with a priority that is not a configured level is dropped, and an error logged.</summary>
    TPriority GetPriority(TFrame frame);

    /// <summary>Gets how many copies of the received message <paramref name="frame"/> are printed while the print manager's "print received" toggle is on: <c>0</c> to not print it, <c>1</c> to print it once, and so on.</summary>
    int GetPrintCount(TFrame frame);

    /// <summary>Gets the short tag identifying the type of message <paramref name="frame"/> is, or an empty string for none.</summary>
    string GetTag(TFrame frame);

    /// <summary>Gets the security level <paramref name="frame"/> was sent at, or <see langword="null"/> for none. A message received with a level that is not a configured one is dropped, and an error logged.</summary>
    TLevel? GetSecurityLevel(TFrame frame);
}
