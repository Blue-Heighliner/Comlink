namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Creates the <see cref="IPeerTransport"/> a peer service runs on, once a current user exists to resolve an identity certificate for.</summary>
internal interface IPeerTransportFactory
{
    /// <summary>Creates a new transport. The caller owns it and must dispose it.</summary>
    IPeerTransport Create();
}

/// <summary>Builds a <see cref="CompositePeerTransport"/> of MSMT (IP) and MicroGate (serial), leaving out IP when no identity certificate is available, wraps it in a <see cref="PacketizingPeerTransport"/> when <see cref="IEngineController.PacketType"/> is set, and finally in an <see cref="IdentifyingPeerTransport"/> that identifies each connection.</summary>
internal sealed class PeerTransportFactory(
    IMsmtSessionPeer.IFactory msmtFactory,
    IMicroGatePeerFactory microGateFactory,
    IEngineController engineController,
    ILoggerFactory loggerFactory) : IPeerTransportFactory
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

        IPeerTransport transport = new CompositePeerTransport(ip, new SerialPeerTransport(microGateFactory, logger, options: engineController.MicroGateOptions));
        if (packetizer is not null) { transport = new PacketizingPeerTransport(transport, packetizer, engineController.PacketWindow, logger); }
        return CreateIdentifying(transport, logger);
    }

    private IdentifyingPeerTransport CreateIdentifying(IPeerTransport transport, ILogger logger)
    {
        try
        {
            return new IdentifyingPeerTransport(transport, engineController, logger);
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
