namespace BlueHeighliner.Comlink;

/// <summary>Manages inbound and outbound peer connections and exposes Engine-level delivery events.</summary>
internal interface IPeerService
{
    /// <summary>Raised when a remote user delivers a new (non-receipt) frame to this user.</summary>
    event Func<object, Task>? FrameDelivered;
    /// <summary>
    /// Raised when a remote user delivers a read receipt instead of an ordinary frame. Carries the ID of
    /// the message that was read and the reading user's name; not raised via <see cref="FrameDelivered"/>.
    /// </summary>
    event Func<string, string, Task>? ReadReceiptReceived;
    /// <summary>
    /// Raised when a remote user delivers a receive receipt instead of an ordinary frame. Carries the ID of
    /// the message that arrived and the receiving user's name; not raised via <see cref="FrameDelivered"/>.
    /// </summary>
    event Func<string, string, Task>? ReceiveReceiptReceived;
    /// <summary>Raised whenever the delivery status of a message sent to a specific user changes.</summary>
    event Func<string, string, DestinationStatus, Task>? DeliveryStatusChanged;
    /// <summary>Raised when a user goes from having no live connection to having at least one.</summary>
    event Func<string, Task>? UserConnected;
    /// <summary>Raised when a user goes from having at least one live connection to having none.</summary>
    event Func<string, Task>? UserDisconnected;
    /// <summary>Returns the names of every user currently reachable over at least one live connection.</summary>
    IReadOnlyList<string> GetConnectedUsers();
    /// <summary>Whether <paramref name="userName"/> is currently reachable over at least one live connection, without allocating the full list <see cref="GetConnectedUsers"/> would.</summary>
    bool IsUserConnected(string userName);
    /// <summary>Starts the inbound peer listener and blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
    /// <summary>Sends <paramref name="message"/> (an instance of <see cref="IEngineController.FrameType"/>) to the peer identified by <paramref name="userName"/>. A server or relay composes no messages, only transports them, so for them this always returns <see langword="false"/>.</summary>
    Task<bool> Send(string userName, object message, CancellationToken cancellation = default);
    /// <summary>
    /// Sends <paramref name="packet"/> (an instance of <see cref="IEngineController.PacketType"/>) directly to the
    /// peer identified by <paramref name="userName"/>, serialized via <see cref="IEngineController.PacketSerializer"/>
    /// instead of <see cref="IEngineController.FrameSerializer"/> - bypassing the normal packetization/reassembly a
    /// full message goes through, and carrying no delivery-status tracking of its own.
    /// </summary>
    Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default);
    /// <summary>Raises <see cref="FrameDelivered"/> directly with <paramref name="payload"/>, without a network round-trip. Used for a message an external system delivers to this user.</summary>
    Task DeliverLocal(object payload);
}
