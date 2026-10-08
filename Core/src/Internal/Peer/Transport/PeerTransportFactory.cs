namespace BlueHeighliner.Comlink;

/// <summary>Creates the <see cref="IPeerTransport"/> a peer service runs on, once a current user exists to resolve an identity certificate for.</summary>
internal interface IPeerTransportFactory
{
    /// <summary>Creates a new transport. The caller owns it and must dispose it.</summary>
    IPeerTransport Create();
}

/// <summary>Builds a <see cref="CompositePeerTransport"/> of MSMT (IP) and MicroGate (serial), leaving out IP when no identity certificate is available, wraps it in a <see cref="PacketizingPeerTransport"/> when <see cref="IEngineController.PacketType"/> is set, and finally in a <see cref="HandshakePeerTransport"/> that carries out the initial message exchange and identifies each connection. An initial packet exchange, when configured, is carried out by another <see cref="HandshakePeerTransport"/> beneath the packetizer. A <see cref="TracingPeerTransport"/>, which writes only while its trace category is on, traces the packets beneath all of that and the frames between the packetizer and the final handshake.</summary>
internal sealed class PeerTransportFactory(
    IMsmtSessionPeer.IFactory msmtFactory,
    IHdlcPeerFactory microGateFactory,
    IEngineController engineController,
    ILoggerFactory loggerFactory,
    ILogSettings logSettings,
    IEngineContextFactory? contexts = null) : IPeerTransportFactory
{
    /// <inheritdoc />
    public IPeerTransport Create()
    {
        ILogger logger = loggerFactory.CreateLogger(LogCategories.App);
        IPacketizer? packetizer = CreatePacketizer();
        if (packetizer is not null && engineController.PacketSize > engineController.HdlcOptions.MaxInfoField)
        {
            logger.Record(LogEvents.PacketSizeExceedsHdlc, "The packet size of {PacketSize} bytes is larger than the HDLC MaxInfoField of {MaxInfoField} bytes, so packets will fail to send over serial connections", engineController.PacketSize, engineController.HdlcOptions.MaxInfoField);
        }

        IPeerTransport? ip = null;
        try
        {
            ip = new MsmtPeerTransport(msmtFactory.Create(engineController.ConnectionOptions), logger);
        }
        catch (InvalidOperationException ex)
        {
            logger.Record(LogEvents.IpConnectionsUnavailable, "IP connections are unavailable: {Message}", ex.Message);
        }

        IPeerTransport transport = new CompositePeerTransport(ip, new SerialPeerTransport(microGateFactory, logger, options: engineController.HdlcOptions));
        if (packetizer is not null)
        {
            transport = new TracingPeerTransport(transport, loggerFactory.CreateLogger(LogCategories.Packets), logSettings, LogCategories.Packets, LogEvents.PacketSent, LogEvents.PacketReceived);
        }
        // The initial packet travels as a packet of its own, so its exchange happens beneath the packetizer; the initial frame is a frame like
        // any other, so its exchange, and identification, happen above it.
        if (packetizer is null && engineController.InitialPacketProcessor is not null)
        {
            throw new InvalidEngineConfigurationException("An initial packet needs a packet type, but none is configured");
        }
        if (packetizer is not null)
        {
            if (Handshake.ForPackets(engineController) is { } initialPacket)
            {
                transport = new HandshakePeerTransport(transport, engineController, logger, initialPacket, identify: false, contexts: contexts);
            }
            transport = new PacketizingPeerTransport(transport, packetizer, engineController.PacketWindow, logger);
        }

        transport = new TracingPeerTransport(transport, loggerFactory.CreateLogger(LogCategories.Frames), logSettings, LogCategories.Frames, LogEvents.FrameSent, LogEvents.FrameReceived);

        return new HandshakePeerTransport(transport, engineController, logger, Handshake.ForFrames(engineController), identify: true, contexts: contexts);
    }

    private IPacketizer? CreatePacketizer()
    {
        if (engineController.PacketType is null)
        {
            return null;
        }

        if (engineController.PacketWindow < 1)
        {
            throw new InvalidEngineConfigurationException($"PacketWindow {engineController.PacketWindow} must be at least 1");
        }

        try { return new Packetizer(engineController); }
        catch (InvalidOperationException ex) when (ex is not InvalidEngineConfigurationException) { throw new InvalidEngineConfigurationException(ex.Message, ex); }
    }
}
