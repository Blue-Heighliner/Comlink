namespace BlueHeighliner.Comlink.Tests;

/// <summary>
/// A <see cref="TestPacketEngineController"/> whose packets serialize to a fixed 16 byte header followed by the data
/// (see <see cref="RawPacketSerializer"/>), so a test can say exactly how much payload fits in a packet of a given size.
/// </summary>
internal class RawPacketEngineController(int payloadSize = 16 * 1024, int packetWindow = 1) : TestPacketEngineController
{
    /// <inheritdoc />
    public override IPacketSerializer? PacketSerializer { get; } = new RawPacketSerializer();

    /// <inheritdoc />
    public override int MaxPayloadSize => payloadSize;

    /// <inheritdoc />
    public override int PacketWindow => packetWindow;
}
