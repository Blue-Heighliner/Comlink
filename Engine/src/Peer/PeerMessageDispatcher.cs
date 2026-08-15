namespace BlueHeighliner.Comlink.Engine.Peer;

/// <summary>
/// Shared deserialize-and-classify logic for raw bytes received over a peer or client connection:
/// distinguishes a user-read confirmation from an ordinary message and raises the matching event.
/// Used by both <see cref="PeerService"/> and <see cref="ClientPeerService"/>.
/// </summary>
internal static class PeerMessageDispatcher
{
    /// <summary>
    /// Deserializes <paramref name="data"/> as an instance of <see cref="IEngineController.MessageType"/> and
    /// raises <paramref name="confirmationReceived"/> or <paramref name="messageDelivered"/> as appropriate.
    /// </summary>
    /// <param name="data">The raw, already-received message payload.</param>
    /// <param name="engineController">Maps logical fields onto the engine's message type.</param>
    /// <param name="logger">Logger for activity messages.</param>
    /// <param name="messageDelivered">Raised with the deserialized message when it is not a confirmation.</param>
    /// <param name="confirmationReceived">Raised with the confirmed message ID and confirming user when it is a confirmation.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> deserialized successfully; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> Dispatch(
        ReadOnlyMemory<byte> data,
        IEngineController engineController,
        ILogger logger,
        Func<object, Task>? messageDelivered,
        Func<string, string, Task>? confirmationReceived)
    {
        try
        {
            object? message = PeerSerializer.Deserialize(engineController.MessageType, data);
            if (message is null) { return false; }

            string confirmationMessageId = engineController.GetConfirmationMessageId(message);
            if (!string.IsNullOrEmpty(confirmationMessageId))
            {
                string confirmingUser = engineController.GetFromUser(message);
                logger.LogInformation("{MessageId} read confirmation received from {User}", confirmationMessageId, confirmingUser);
                if (confirmationReceived is not null)
                {
                    await confirmationReceived(confirmationMessageId, confirmingUser);
                }
                return true;
            }

            logger.LogInformation("{MessageId} received from {FromUser}", engineController.GetMessageId(message), engineController.GetFromUser(message));
            if (messageDelivered is not null)
            {
                await messageDelivered(message);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
