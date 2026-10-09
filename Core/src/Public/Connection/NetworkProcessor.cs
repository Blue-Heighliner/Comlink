namespace BlueHeighliner.Comlink;

/// <summary>
/// Runs the host's own protocol, independent of any UI. The engine only transports frames and keeps the user's messages: it does not receive, route, receipt or retrieve anything itself and does not keep the
/// network indicator, so what happens when a user sends a message, a frame arrives or a user asks for stored messages is whatever this processor does, with the engine's help through the context each method is
/// handed (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}"/>). A method runs in the background: it is not awaited by the engine, and an exception it throws is logged rather than thrown back. Every method has a default that
/// does nothing, so a processor states only what it takes part in. State one with <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel, TAspect}.Frames{TProcessor}"/>.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
public interface INetworkProcessor<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Called when a user goes from having no live peer connection to having at least one. <see cref="INetworkConnectedContext{TFrame, TPriority, TLevel, TAspect}.TargetUser"/> names the user that connected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnConnected(INetworkConnectedContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;

    /// <summary>Called when a user goes from having at least one live peer connection to having none. <see cref="INetworkDisconnectedContext{TFrame, TPriority, TLevel, TAspect}.TargetUser"/> names the user that disconnected.</summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnDisconnected(INetworkDisconnectedContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;

    /// <summary>
    /// Called for every frame this node receives, from a peer, from the local interface or from an external system (see <see cref="INetworkReceivedContext{TFrame, TPriority, TLevel, TAspect}.Origin"/>), except the heartbeats that keep connections live.
    /// What it is, a message, a receipt, a request, is for the processor to tell: a message is recorded with <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.ReceiveMessage"/>, anything else is acted on, sent on or dropped.
    /// </summary>
    /// <param name="context">A snapshot of the engine taken for this event.</param>
    Task OnReceived(INetworkReceivedContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;

    /// <summary>
    /// Called when the user sends a message with the GUI, after the engine has stored it in the Outbox with no delivery status yet and marked the destinations it cannot be sent to as failed. It is not called when no
    /// destination is left (see <see cref="INetworkSentContext{TFrame, TPriority, TLevel, TAspect}.Destinations"/>). The processor sends the message however it sees fit, one frame per destination or one for all,
    /// and reports each destination's outcome with <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SetSentStatus(string, string, DestinationStatus)"/>.
    /// </summary>
    /// <param name="context">A snapshot of the engine taken for this event, holding the message.</param>
    Task OnSent(INetworkSentContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;

    /// <summary>
    /// Called when the user opens a message they received, after the engine has marked it read. The processor tells the sender if it wants to, for example with a read receipt frame.
    /// </summary>
    /// <param name="context">A snapshot of the engine taken for this event, holding the message.</param>
    Task OnRead(INetworkReadContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;

    /// <summary>
    /// Called when the user submits a retrieval with the GUI: a request to a server for the messages it stored that fit some criteria. The processor sends the request to the server, and the server's own processor answers it
    /// (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.FindStoredMessages"/>); what comes back arrives as received frames.
    /// </summary>
    /// <param name="context">A snapshot of the engine taken for this event, holding the criteria and the server they are asked of.</param>
    Task OnRetrieval(INetworkRetrievalContext<TFrame, TPriority, TLevel, TAspect> context) => Task.CompletedTask;
}
