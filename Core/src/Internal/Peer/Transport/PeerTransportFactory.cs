namespace BlueHeighliner.Comlink;

/// <summary>Creates the <see cref="IPeerTransport"/> a peer service runs on, once a current user exists to resolve an identity certificate for.</summary>
internal interface IPeerTransportFactory
{
    /// <summary>Creates a new transport. The caller owns it and must dispose it.</summary>
    IPeerTransport Create();
}

/// <summary>Builds a <see cref="CompositePeerTransport"/> of MSMT (IP) and MicroGate (serial), leaving out IP when no identity certificate is available, wraps it in a <see cref="PacketizingPeerTransport"/> when <see cref="IEngineController.PacketType"/> is set, and finally in a <see cref="HandshakePeerTransport"/> that carries out the initial message exchange and identifies each connection. An initial packet exchange, when configured, is carried out by another <see cref="HandshakePeerTransport"/> beneath the packetizer.</summary>
internal sealed class PeerTransportFactory(
    IMsmtSessionPeer.IFactory msmtFactory,
    IHdlcPeerFactory microGateFactory,
    IEngineController engineController,
    ILoggerFactory loggerFactory,
    IEngineContextFactory? contexts = null) : IPeerTransportFactory
{
    /// <inheritdoc />
    public IPeerTransport Create()
    {
        ILogger logger = loggerFactory.CreateLogger("ACTIVITY");
        IPacketizer? packetizer = CreatePacketizer(logger);

        IPeerTransport? ip = null;
        try
        {
            ip = new MsmtPeerTransport(msmtFactory.Create(engineController.ConnectionOptions));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("IP connections are unavailable: {Message}", ex.Message);
        }

        IPeerTransport transport = new CompositePeerTransport(ip, new SerialPeerTransport(microGateFactory, logger, options: engineController.HdlcOptions));
        try
        {
            // The initial packet travels as a packet of its own, so its exchange happens beneath the packetizer; the initial frame is a frame like
            // any other, so its exchange, and identification, happen above it.
            if (packetizer is null && engineController.InitialPacketProcessor is not null) { throw new InvalidOperationException("An initial packet needs a packet type, but none is configured"); }
            if (packetizer is not null)
            {
                if (Handshake.ForPackets(engineController) is { } initialPacket) { transport = new HandshakePeerTransport(transport, engineController, logger, initialPacket, identify: false, contexts: contexts); }
                transport = new PacketizingPeerTransport(transport, packetizer, engineController.PacketWindow, logger);
            }

            return new HandshakePeerTransport(transport, engineController, logger, Handshake.ForFrames(engineController), identify: true, contexts: contexts);
        }
        catch (Exception ex)
        {
            logger.LogError("Connection identification is misconfigured, so networking cannot start: {Message}", ex.Message);
            throw;
        }
    }

    // Logged as well as thrown because the peer services start on a background task, where a throw alone would go unseen.
    private IPacketizer? CreatePacketizer(ILogger logger)
    {
        if (engineController.PacketType is null) { return null; }

        try
        {
            if (engineController.PacketWindow < 1) { throw new InvalidOperationException($"PacketWindow {engineController.PacketWindow} must be at least 1"); }

            return new Packetizer(engineController);
        }
        catch (Exception ex)
        {
            logger.LogError("Packetization is misconfigured, so networking cannot start: {Message}", ex.Message);
            throw;
        }
    }
}
