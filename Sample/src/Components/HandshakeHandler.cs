namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Identifies the node on the other end of every connection with a handshake of packets. The engine does not say who starts, so this handler decides: the node that
/// opened an IP connection, or on a serial cable (where both ends open the port) the node at the higher station address, sends a <see cref="Packet"/> whose chunk is its user name,
/// the other node answers with one carrying its own, and each marks the connection connected as the user the other named, so a serial link needs no <c>User</c> on its
/// outgoing point and an IP connection does not depend on certificate names.
/// </summary>
public sealed class HandshakeHandler : IPacketHandshakeHandler<Packet>
{
    /// <inheritdoc />
    public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task OnConnected(IPacketHandshakeContext<Packet> context)
    {
        if (Starts(context))
        {
            await context.Send(Announce(context));
        }
    }

    /// <inheritdoc />
    public async Task OnReceived(IPacketHandshakeContext<Packet> context, Packet packet)
    {
        if (!Starts(context))
        {
            await context.Send(Announce(context));
        }

        if (packet.Chunk.Length == 0)
        {
            await context.Disconnect();
        }
        else
        {
            await context.Connected(Encoding.UTF8.GetString(packet.Chunk));
        }
    }

    private bool Starts(IPacketHandshakeContext<Packet> context)
        => context.Connection is ISerialConnectionInfo serial ? serial.SerialAddress > serial.RemoteSerialAddress : !context.Connection.IsInbound;

    private Packet Announce(IPacketHandshakeContext<Packet> context) => new() { Chunk = Encoding.UTF8.GetBytes(context.Connection.LocalUser ?? string.Empty) };
}
