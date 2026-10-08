namespace BlueHeighliner.Comlink;

/// <summary>
/// Shared fire-and-forget dispatch for <see cref="IPeerService.UserConnected"/>/<see cref="IPeerService.UserDisconnected"/>:
/// invokes every subscriber on a background task and logs, rather than throws, if any of them fail, the same way
/// delivery-status and message-received events are raised. Used by <see cref="ClientPeerService"/> and <see cref="ServerRoutingService"/>.
/// </summary>
internal static class PeerConnectionNotifier
{
    /// <summary>Invokes every <paramref name="subscribers"/> handler with <paramref name="userName"/> on a background task. Does nothing for an empty user name or no subscribers.</summary>
    /// <param name="subscribers">The event's current subscribers.</param>
    /// <param name="userName">The user that connected or disconnected.</param>
    /// <param name="action">Describes the transition for the failure log message (e.g. <c>"connecting"</c>, <c>"disconnecting"</c>).</param>
    /// <param name="logger">Logger for a handler failure.</param>
    public static void Raise(Func<string, Task>? subscribers, string userName, string action, ILogger logger)
    {
        if (subscribers is null || string.IsNullOrEmpty(userName))
        {
            return;
        }
        _ = Task.Run(async () =>
        {
            try { await subscribers.InvokeAll(userName); }
            catch (Exception ex) { logger.Record(LogEvents.ConnectionEventHandlerFailed, ex, "Failed to handle {UserName} {Action}", userName, action); }
        });
    }
}
