namespace BlueHeighliner.Comlink.Tests;

/// <summary>Recognizes and builds the heartbeat a <see cref="PeerConnectionMonitor"/> sends: a serialized empty instance of the test frame type.</summary>
internal static class TestHeartbeat
{
    private static readonly TestEngineController controller = new();
    private static readonly Lock gate = new();

    /// <summary>Returns the bytes of a heartbeat.</summary>
    public static byte[] Bytes()
    {
        lock (gate)
        {
            using IMemoryOwner<byte> owner = controller.FrameSerializer.Serialize(Frame());
            return owner.Memory.ToArray();
        }
    }

    /// <summary>Returns whether <paramref name="payload"/> is a heartbeat rather than a real message.</summary>
    /// <param name="payload">What was sent over a connection.</param>
    public static bool Is(ReadOnlyMemory<byte> payload)
    {
        lock (gate)
        {
            try { return controller.FrameSerializer.Deserialize(payload, null) is { } message && controller.IsHeartbeat(message); }
            catch (Exception) { return false; }
        }
    }

    private static object Frame()
    {
        return controller.CreateHeartbeat();
    }
}
