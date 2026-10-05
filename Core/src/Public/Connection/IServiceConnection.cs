namespace BlueHeighliner.Comlink;

/// <summary>High-level client API for interacting with a running Engine service instance.</summary>
public interface IServiceConnection
{
    /// <summary>Raised when a new inbound message arrives.</summary>
    event Func<MessageReceivedEvent, Task>? MessageReceived;
    /// <summary>Raised when the delivery status of an outbound message changes.</summary>
    event Func<DeliveryStatusChangedEvent, Task>? DeliveryStatusChanged;
    /// <summary>Establishes the connection to the Engine service.</summary>
    Task Connect(CancellationToken cancellation = default);
    /// <summary>Returns this user's own <see cref="UserInfo"/>, or <see langword="null"/> if not yet registered.</summary>
    Task<UserInfo?> GetUserInfo(CancellationToken cancellation = default);
    /// <summary>Returns the names of all known users in the messaging system.</summary>
    Task<List<string>> GetUserNames(CancellationToken cancellation = default);
    /// <summary>Returns the names of every user currently reachable over at least one live peer connection, unlike <see cref="GetUserNames"/>'s fixed configured directory.</summary>
    Task<List<string>> GetConnectedUsers(CancellationToken cancellation = default);
    /// <summary>Registers this instance as a user using <paramref name="userCode"/> and returns the resulting <see cref="UserInfo"/>.</summary>
    Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default);
    /// <summary>
    /// Sends a message with the given <paramref name="body"/> to the specified
    /// <paramref name="addresses"/>. Whether the message is an alert, which alarms recipients' Client-mode UI until it is read (see <c>Docs/Components/ViewModels.md</c>), is not
    /// chosen here: the host's message handler decides it from the message's other properties (see <see cref="SendMessageResult.IsAlert"/>). <paramref name="priority"/>
    /// is used verbatim as the MSMT send priority (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}"/>). <paramref name="tag"/>
    /// is stored in the message's tag field (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}"/>). <paramref name="securityLevel"/>
    /// is the security level this message is sent at, a member of the enum the host stated for its security levels (or <see langword="null"/> for none), which must be a configured one or the call throws; a destination user whose own assigned level ranks lower
    /// is never sent the message (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}"/>).
    /// </summary>
    Task<SendMessageResult?> SendMessage(string body, List<AddressRequest> addresses, Enum? priority = null, string tag = "", Enum? securityLevel = null, CancellationToken cancellation = default);
    /// <summary>
    /// Marks the Inbox record for <paramref name="messageId"/> as read (no-op if already read or not
    /// found) and sends a read receipt frame back to the original sender so it can advance
    /// that message's Outbox delivery status to <see cref="DestinationStatus.Read"/>. Returns
    /// <see langword="true"/> if the record's read state actually changed.
    /// </summary>
    Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
}
