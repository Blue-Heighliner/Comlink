namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Identifies the node on the other end of every connection with an initial packet exchange: the opening node sends a <see cref="Packet"/>
/// whose chunk is its user name, the accepting node answers with one carrying its own, and each marks the connection connected as the user the
/// other named, so a serial link needs no <c>User</c> on its outgoing point and an IP connection does not depend on certificate names.
/// </summary>
public sealed class IdentityProcessor : IInitialPacketProcessor<Packet>
{
    /// <inheritdoc />
    public void OnConnected(IInitialPacketContext<Packet> context)
    {
        if (context.IsOpener) { context.Send(Announce(context)); }
    }

    /// <inheritdoc />
    public void OnInitial(IInitialPacketContext<Packet> context, Packet packet)
    {
        context.Send(Announce(context));
        Identify(context, packet);
    }

    /// <inheritdoc />
    public void OnReply(IInitialPacketContext<Packet> context, Packet packet) => Identify(context, packet);

    private Packet Announce(IInitialPacketContext<Packet> context) => new() { Chunk = Encoding.UTF8.GetBytes(context.Connection.LocalUser ?? string.Empty) };

    private void Identify(IInitialPacketContext<Packet> context, Packet packet)
    {
        if (packet.Chunk.Length == 0) { context.Disconnect(); }
        else { context.Connected(Encoding.UTF8.GetString(packet.Chunk)); }
    }
}
