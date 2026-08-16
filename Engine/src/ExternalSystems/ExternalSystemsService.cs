namespace BlueHeighliner.Comlink.Engine.ExternalSystems;

/// <summary>
/// Coordinates every configured <see cref="IExternalSystem"/> (see <see cref="Control.IEngineController.ExternalSystems"/>):
/// runs each one's own connect/poll/disconnect lifecycle, routes every message the app receives (from a
/// peer, or from any other external system) out to other external systems, and processes every message
/// received from an external system exactly like an ordinary received message — see
/// <c>Docs/ExternalSystems.md</c>. When <see cref="Control.IEngineController.ExternalServer"/> is set, a
/// message not received from it is instead sent exclusively to it; a message received from it is routed
/// to every other external system exactly as it would be without one configured.
/// </summary>
internal interface IExternalSystemsService
{
    /// <summary>Starts every configured external system's connect/poll/disconnect lifecycle and wires up message routing. Blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IExternalSystemsService" />
internal sealed class ExternalSystemsService : IExternalSystemsService
{
    /// <summary>Initializes a new <see cref="ExternalSystemsService"/>, resolving the configured external systems once.</summary>
    public ExternalSystemsService(IEngineController engineController, IPeerService peerService, ILoggerFactory loggerFactory)
    {
        this.peerService = peerService;
        systems = engineController.ExternalSystems;
        externalServer = engineController.ExternalServer;
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerService peerService;
    private readonly IReadOnlyList<IExternalSystem> systems;
    private readonly IExternalSystem? externalServer;
    private readonly ILogger logger;
    private readonly AsyncLocal<IExternalSystem?> receivingFrom = new();

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (systems.Count == 0) { return; }

        peerService.MessageDelivered += RouteToExternalSystems;
        foreach (IExternalSystem system in systems)
        {
            system.AttachLogger(logger);
            system.MessageReceived += message => OnExternalSystemMessageReceived(system, message);
        }

        try
        {
            await Task.WhenAll(systems.Select(system => RunSystem(system, cancellation)));
        }
        finally
        {
            peerService.MessageDelivered -= RouteToExternalSystems;
        }
    }

    private async Task RunSystem(IExternalSystem system, CancellationToken cancellation)
    {
        try { await system.Start(cancellation); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogError(ex, "External system {Name} stopped unexpectedly", system.Name); }
    }

    private async Task OnExternalSystemMessageReceived(IExternalSystem source, object message)
    {
        // Marks this logical call chain as "currently routing a message received from `source`" so that
        // RouteToExternalSystems — invoked synchronously underneath DeliverLocal, below — knows where the
        // message came from. AsyncLocal (rather than a plain field) keeps this correct if more than one
        // external system receives a message concurrently.
        receivingFrom.Value = source;
        try
        {
            // Processes the message exactly like an ordinary received message (stored, mirrored to
            // interfaces, shown in the UI) via the same path a real peer delivery uses.
            await peerService.DeliverLocal(message);
        }
        finally
        {
            receivingFrom.Value = null;
        }
    }

    private async Task RouteToExternalSystems(object message)
    {
        IExternalSystem? source = receivingFrom.Value;

        // A message not received from the external server (composed locally by the user, received from a
        // peer connection, or received from any other external system) goes exclusively to it, bypassing
        // every other configured external system.
        if (externalServer is not null && !ReferenceEquals(source, externalServer))
        {
            await externalServer.Send(message);
            return;
        }

        foreach (IExternalSystem system in systems)
        {
            if (ReferenceEquals(system, source)) { continue; }
            await system.Send(message);
        }
    }
}
