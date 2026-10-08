namespace BlueHeighliner.Comlink;

/// <summary>Shared deserialize logic for raw bytes received over a peer connection: turns them into a frame of the configured type and hands it on, leaving what it means to the host's network processor. Used by <see cref="ClientPeerService"/> and <see cref="ServerRoutingService"/>.</summary>
internal static class PeerFrameDispatcher
{
    /// <summary>
    /// Deserializes <paramref name="data"/> as an instance of <see cref="IEngineController.FrameType"/> and raises <paramref name="frameReceived"/> with it. A <see cref="PeerConnectionMonitor"/> heartbeat (a frame the host's
    /// heartbeat handler recognizes, see <see cref="IEngineController.IsHeartbeat"/>) is not a real frame and is acknowledged without being raised.
    /// </summary>
    /// <param name="data">The raw, already-received frame payload.</param>
    /// <param name="engineController">Supplies the serializer and recognizes heartbeats.</param>
    /// <param name="frameReceived">Raised with the frame and who it arrived from.</param>
    /// <param name="sourceUser">The user at the other end of the connection the bytes arrived over.</param>
    /// <param name="packet">The first packet that carried the frame across, or <see langword="null"/> when it did not travel in packets.</param>
    /// <returns><see langword="true"/> if <paramref name="data"/> deserialized successfully or was an ignored heartbeat; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> Dispatch(ReadOnlyMemory<byte> data, IEngineController engineController, Func<ReceivedFrame, Task>? frameReceived, string sourceUser, object? packet = null)
    {
        try
        {
            // The serializer determines the type from the data itself, so bytes from an incompatible sender could describe a type other than
            // this node's own frame type; that is a failed deserialize, not something to hand to a processor that would cast it.
            object frame = engineController.FrameSerializer.Deserialize(data, packet);
            if (frame.GetType() != engineController.FrameType)
            {
                return false;
            }

            if (engineController.IsHeartbeat(frame))
            {
                return true;
            }

            await frameReceived.InvokeAll(new ReceivedFrame(frame, sourceUser));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
