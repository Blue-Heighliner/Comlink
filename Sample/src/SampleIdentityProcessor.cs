namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Identifies the node on the other end of every connection with an initial packet exchange: the opening node sends a <see cref="SamplePacket"/>
/// whose chunk is its user name, the accepting node answers with one carrying its own, and each marks the connection connected as the user the
/// other named, so a serial link needs no <c>User</c> on its outgoing point and an IP connection does not depend on certificate names.
/// </summary>
public sealed class SampleIdentityProcessor : IInitialPacketProcessor<SamplePacket>
{
    /// <inheritdoc />
    public async Task OnConnected(IInitialPacketContext<SamplePacket> context)
    {
        if (context.IsOpener) { await context.Send(Announce(context)); }
    }

    /// <inheritdoc />
    public async Task OnInitial(IInitialPacketContext<SamplePacket> context, SamplePacket packet)
    {
        await context.Send(Announce(context));
        Identify(context, packet);
    }

    /// <inheritdoc />
    public Task OnReply(IInitialPacketContext<SamplePacket> context, SamplePacket packet)
    {
        Identify(context, packet);
        return Task.CompletedTask;
    }

    private SamplePacket Announce(IInitialPacketContext<SamplePacket> context) => new() { Chunk = Encoding.UTF8.GetBytes(context.Connection.LocalUser ?? string.Empty) };

    private void Identify(IInitialPacketContext<SamplePacket> context, SamplePacket packet)
    {
        if (packet.Chunk.Length == 0) { context.Disconnect(); }
        else { context.Connected(Encoding.UTF8.GetString(packet.Chunk)); }
    }
}
