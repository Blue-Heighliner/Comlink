namespace BlueHeighliner.Comlink;

/// <summary>A frame a peer delivered, with the user it arrived from.</summary>
/// <param name="Frame">An instance of <see cref="IEngineController.FrameType"/>.</param>
/// <param name="SourceUser">The name of the user at the other end of the connection it arrived over: the one that handed it on, who is not necessarily who wrote it.</param>
internal sealed record ReceivedFrame(object Frame, string SourceUser);

/// <summary>Manages inbound and outbound peer connections and exposes the frames that arrive over them. It only transports: what a frame means and where it goes next is the host's network processor's.</summary>
internal interface IPeerService
{
    /// <summary>Raised when a peer delivers a frame to this node; heartbeats, which only keep a connection live, are never raised.</summary>
    event Func<ReceivedFrame, Task>? FrameReceived;
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
    /// <summary>
    /// Sends <paramref name="frame"/> (an instance of <see cref="IEngineController.FrameType"/>) to the user <paramref name="userName"/>, who must be directly connected to this node, over the connection identified as them, and completes once the transport has fully acknowledged it.
    /// Nothing is routed or relayed: reaching a user who is not directly connected is up to the host's network processor, which sends to the node that is.
    /// </summary>
    /// <param name="userName">The user to send it to.</param>
    /// <param name="frame">What to send.</param>
    /// <param name="priority">The send priority, see <see cref="IEngineController.SendPriority"/>.</param>
    /// <param name="cancellation">Cancels the send.</param>
    /// <returns><see langword="true"/> if the frame was accepted, <see langword="false"/> if the user is not directly connected or the frame was not accepted.</returns>
    Task<bool> Send(string userName, object frame, int priority, CancellationToken cancellation = default);
    /// <summary>
    /// Sends <paramref name="packet"/> (an instance of <see cref="IEngineController.PacketType"/>) directly to the
    /// peer identified by <paramref name="userName"/>, serialized via <see cref="IEngineController.PacketSerializer"/>
    /// instead of <see cref="IEngineController.FrameSerializer"/> - bypassing the normal packetization/reassembly a
    /// full message goes through.
    /// </summary>
    Task<bool> SendPacket(string userName, object packet, CancellationToken cancellation = default);
}
