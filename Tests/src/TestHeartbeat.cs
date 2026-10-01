namespace BlueHeighliner.Comlink.Tests;

/// <summary>Recognizes and builds the heartbeat a <see cref="PeerConnectionMonitor"/> sends: a serialized empty instance of the test message type.</summary>
internal static class TestHeartbeat
{
    private static readonly TestEngineController controller = new();
    private static readonly Lock gate = new();

    /// <summary>Returns the bytes of a heartbeat.</summary>
    public static byte[] Bytes()
    {
        lock (gate)
        {
            using IMemoryOwner<byte> owner = controller.NetworkSerializer.Serialize(controller.CreateMessage());
            return owner.Memory.ToArray();
        }
    }

    /// <summary>Returns whether <paramref name="payload"/> is a heartbeat rather than a real message.</summary>
    /// <param name="payload">What was sent over a connection.</param>
    public static bool Is(ReadOnlyMemory<byte> payload)
    {
        lock (gate)
        {
            try { return controller.NetworkSerializer.Deserialize(payload) is { } message && controller.IsHeartbeat(message); }
            catch (Exception) { return false; }
        }
    }
}
