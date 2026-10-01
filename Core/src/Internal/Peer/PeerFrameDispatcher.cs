namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Shared deserialize-and-classify logic for raw bytes received over a peer or client connection:
/// distinguishes a user-read confirmation from an ordinary frame and raises the matching event.
/// Used by both <see cref="PeerService"/> and <see cref="ClientPeerService"/>.
/// </summary>
internal static class PeerFrameDispatcher
{
    /// <summary>
    /// Deserializes <paramref name="data"/> as an instance of <see cref="IEngineController.FrameType"/> and
    /// raises <paramref name="confirmationReceived"/> or <paramref name="frameDelivered"/> as appropriate.
    /// A <see cref="PeerConnectionMonitor"/> heartbeat (an empty frame that is not a message, see <see cref="EngineControllerExtensions.IsHeartbeat"/>)
    /// is not a real frame and is acknowledged without being delivered.
    /// </summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <param name="engineController">Maps logical fields onto the engine's frame type.</param>
    /// <param name="logger">Logger for activity messages.</param>
    /// <param name="frameDelivered">Raised with the deserialized frame when it is not a confirmation.</param>
    /// <param name="confirmationReceived">Raised with the confirmed message ID and confirming user when it is a confirmation.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> deserialized successfully or was an ignored heartbeat; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> Dispatch(
        ReadOnlyMemory<byte> data,
        IEngineController engineController,
        ILogger logger,
        Func<object, Task>? frameDelivered,
        Func<string, string, Task>? confirmationReceived)
    {
        try
        {
            object? frame = engineController.NetworkSerializer.Deserialize(data);
            if (frame is null) { return false; }

            if (engineController.IsHeartbeat(frame)) { return true; }

            string confirmationMessageId = engineController.GetConfirmationMessageId(frame);
            if (!string.IsNullOrEmpty(confirmationMessageId))
            {
                string confirmingUser = engineController.GetFromUser(frame);
                logger.LogInformation("{MessageId} read confirmation received from {User}", confirmationMessageId, confirmingUser);
                await confirmationReceived.InvokeAll(confirmationMessageId, confirmingUser);
                return true;
            }

            if (engineController.IsRetrieval(frame))
            {
                logger.LogWarning("{MessageId} retrieval request from {User} ignored: only a storage server answers one", engineController.GetFrameId(frame), engineController.GetFromUser(frame));
                return true;
            }

            logger.LogInformation("{MessageId} received from {FromUser}", engineController.GetFrameId(frame), engineController.GetFromUser(frame));
            await frameDelivered.InvokeAll(frame);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
