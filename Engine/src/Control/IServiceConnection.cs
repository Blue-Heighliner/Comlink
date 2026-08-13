namespace BlueHeighliner.Comlink.Engine.Control;

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
    /// <summary>Registers this instance as a user using <paramref name="userCode"/> and returns the resulting <see cref="UserInfo"/>.</summary>
    Task<UserInfo?> InstallUser(string userCode, CancellationToken cancellation = default);
    /// <summary>
    /// Sends a message with the given <paramref name="subject"/> and <paramref name="body"/> to the specified
    /// <paramref name="addresses"/>. When <paramref name="isAlert"/> is <see langword="true"/>, recipients'
    /// Client-mode UI alarms until the message is read; see <c>Docs/ViewModels.md</c>. <paramref name="priority"/>
    /// is used verbatim as the OFT send priority (see <see cref="IMessageFormat.GetPriority"/>). <paramref name="tag"/>
    /// is stored via <see cref="IMessageFormat.SetTag"/>.
    /// </summary>
    Task<SendMessageResult?> SendMessage(string subject, string body, List<AddressRequest> addresses, bool isAlert = false, int priority = 0, string tag = "", CancellationToken cancellation = default);
    /// <summary>
    /// Marks the Inbox record for <paramref name="messageId"/> as read (no-op if already read or not
    /// found) and sends a user-read confirmation message back to the original sender so it can advance
    /// that message's Outbox delivery status to <see cref="DestinationStatus.Read"/>. Returns
    /// <see langword="true"/> if the record's read state actually changed.
    /// </summary>
    Task<bool> MarkMessageRead(string messageId, CancellationToken cancellation = default);
}
