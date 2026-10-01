namespace BlueHeighliner.Comlink.Peer;

/// <summary>
/// Shared deserialize-and-classify logic for raw bytes received over a peer or client connection:
/// distinguishes receive and read receipts from an ordinary frame and raises the matching event.
/// Used by both <see cref="PeerService"/> and <see cref="ClientPeerService"/>.
/// </summary>
internal static class PeerFrameDispatcher
{
    /// <summary>
    /// Deserializes <paramref name="data"/> as an instance of <see cref="IEngineController.FrameType"/> and
    /// raises <paramref name="readReceiptReceived"/>, <paramref name="receiveReceiptReceived"/> or <paramref name="frameDelivered"/> as appropriate.
    /// A <see cref="PeerConnectionMonitor"/> heartbeat (an empty frame that is not a message, see <see cref="EngineControllerExtensions.IsHeartbeat"/>)
    /// is not a real frame and is acknowledged without being delivered.
    /// </summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <param name="engineController">Maps logical fields onto the engine's frame type.</param>
    /// <param name="logger">Logger for activity messages.</param>
    /// <param name="frameDelivered">Raised with the deserialized frame when it is not a receipt.</param>
    /// <param name="readReceiptReceived">Raised with the message ID and reading user when it is a read receipt.</param>
    /// <param name="receiveReceiptReceived">Raised with the message ID and receiving user when it is a receive receipt.</param>
    /// <param name="packet">The first packet that carried the frame across, or <see langword="null"/> when it did not travel in packets.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> deserialized successfully or was an ignored heartbeat; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> Dispatch(
        ReadOnlyMemory<byte> data,
        IEngineController engineController,
        ILogger logger,
        Func<object, Task>? frameDelivered,
        Func<string, string, Task>? readReceiptReceived,
        Func<string, string, Task>? receiveReceiptReceived,
        object? packet = null)
    {
        try
        {
            object frame = engineController.FrameSerializer.Deserialize(data, packet);

            if (engineController.IsHeartbeat(frame)) { return true; }

            if (engineController.IsReadReceipt(frame))
            {
                string readMessageId = engineController.GetReadReceiptMessageId(frame);
                string readingUser = engineController.GetFromUser(frame);
                logger.LogInformation("{MessageId} read receipt received from {User}", readMessageId, readingUser);
                await readReceiptReceived.InvokeAll(readMessageId, readingUser);
                return true;
            }

            if (engineController.IsReceiveReceipt(frame))
            {
                string receivedMessageId = engineController.GetReceiveReceiptMessageId(frame);
                string receivingUser = engineController.GetFromUser(frame);
                logger.LogInformation("{MessageId} receive receipt received from {User}", receivedMessageId, receivingUser);
                await receiveReceiptReceived.InvokeAll(receivedMessageId, receivingUser);
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
