namespace BlueHeighliner.Comlink.Peer.Transport;

/// <summary>Creates the <see cref="IPeerTransport"/> a peer service runs on, once a current user exists to resolve an identity certificate for.</summary>
internal interface IPeerTransportFactory
{
    /// <summary>Creates a new transport. The caller owns it and must dispose it.</summary>
    IPeerTransport Create();
}

/// <summary>Builds a <see cref="CompositePeerTransport"/> of MSMT (IP) and MicroGate (serial), leaving out IP when no identity certificate is available.</summary>
internal sealed class PeerTransportFactory(
    IMsmtPeerFactory msmtFactory,
    IMicroGatePeerFactory microGateFactory,
    IEngineController engineController,
    ILoggerFactory loggerFactory) : IPeerTransportFactory
{
    /// <inheritdoc />
    public IPeerTransport Create()
    {
        ILogger logger = loggerFactory.CreateLogger("ACTIVITY");
        IPeerTransport? ip = null;
        try
        {
            ip = new MsmtPeerTransport(msmtFactory.Create(engineController.ConnectionOptions));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("IP connections are unavailable: {Message}", ex.Message);
        }

        return new CompositePeerTransport(ip, new SerialPeerTransport(microGateFactory, logger));
    }
}
