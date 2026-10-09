namespace BlueHeighliner.Comlink;

/// <summary>Creates the <see cref="IPeerTransport"/> a peer service runs on, once a current user exists to resolve an identity certificate for.</summary>
internal interface IPeerTransportFactory
{
    /// <summary>Creates a new transport. The caller owns it and must dispose it.</summary>
    IPeerTransport Create();
}

/// <summary>Builds a <see cref="CompositePeerTransport"/> of MSMT (IP) and MicroGate (serial), leaving out IP when no identity certificate is available, wraps it in a <see cref="PacketizingPeerTransport"/> when <see cref="IEngineController.PacketType"/> is set, and finally in a <see cref="HandshakePeerTransport"/> that carries out the handshake and identifies each connection. A packet handshake, when configured, is carried out by another <see cref="HandshakePeerTransport"/> beneath the packetizer, and a frame handshake by the final one. A <see cref="TracingPeerTransport"/>, which writes only while its trace category is on, traces the packets beneath all of that and the frames between the packetizer and the final handshake.</summary>
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
        if (packetizer is not null && engineController.MaxPayloadSize >= engineController.HdlcOptions.MaxInfoField)
        {
            logger.Record(LogEvents.MaxPayloadSizeExceedsHdlc, "The payload size of {MaxPayloadSize} bytes leaves no room for a packet's own fields within the HDLC MaxInfoField of {MaxInfoField} bytes, so packets will fail to send over serial connections", engineController.MaxPayloadSize, engineController.HdlcOptions.MaxInfoField);
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
        // A packet handshake's packets travel as packets of their own, so it happens beneath the packetizer; a frame handshake's frames are frames like any other, so it, and identification, happen above it.
        if (packetizer is null && engineController.PacketHandshakeProcessor is not null)
        {
            throw new InvalidEngineConfigurationException("A packet handshake processor needs a packet type, but none is configured");
        }
        if (packetizer is not null)
        {
            if (Handshake.ForPackets(engineController) is { } handshake)
            {
                transport = new HandshakePeerTransport(transport, engineController, logger, handshake, identify: false, contexts: contexts);
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

        if (engineController.MaxPayloadSize < 1)
        {
            throw new InvalidEngineConfigurationException($"MaxPayloadSize {engineController.MaxPayloadSize} must be at least 1");
        }

        if (engineController.PacketWindow < 1)
        {
            throw new InvalidEngineConfigurationException($"PacketWindow {engineController.PacketWindow} must be at least 1");
        }

        try { return new Packetizer(engineController); }
        catch (InvalidOperationException ex) when (ex is not InvalidEngineConfigurationException) { throw new InvalidEngineConfigurationException(ex.Message, ex); }
    }
}
