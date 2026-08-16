namespace BlueHeighliner.Comlink.Engine.ExternalSystems;

/// <summary>
/// Coordinates every configured <see cref="IExternalSystem"/> (see <see cref="Control.IEngineController.GetExternalSystems"/>):
/// runs each one's own connect/poll/disconnect lifecycle, relays every message the app receives (from a
/// peer, or from any other external system) out to every <em>other</em> external system, and processes
/// every message received from an external system exactly like an ordinary received message — see
/// <c>Docs/ExternalSystems.md</c>.
/// </summary>
internal interface IExternalSystemsService
{
    /// <summary>Starts every configured external system's connect/poll/disconnect lifecycle and wires up message relaying. Blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);
}

/// <inheritdoc cref="IExternalSystemsService" />
internal sealed class ExternalSystemsService : IExternalSystemsService
{
    /// <summary>Initializes a new <see cref="ExternalSystemsService"/>, resolving the configured external systems once.</summary>
    public ExternalSystemsService(IEngineController engineController, IPeerService peerService, ILoggerFactory loggerFactory)
    {
        this.peerService = peerService;
        systems = engineController.GetExternalSystems();
        logger = loggerFactory.CreateLogger("ACTIVITY");
    }

    private readonly IPeerService peerService;
    private readonly IReadOnlyList<IExternalSystem> systems;
    private readonly ILogger logger;
    private readonly AsyncLocal<IExternalSystem?> receivingFrom = new();

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (systems.Count == 0) { return; }

        peerService.MessageDelivered += RelayToOtherExternalSystems;
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
            peerService.MessageDelivered -= RelayToOtherExternalSystems;
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
        // Marks this logical call chain as "currently relaying a message received from `source`" so that
        // RelayToOtherExternalSystems — invoked synchronously underneath DeliverLocal, below — knows to
        // skip sending the message back to the very system it just arrived from. AsyncLocal (rather than a
        // plain field) keeps this correct if more than one external system receives a message concurrently.
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

    private async Task RelayToOtherExternalSystems(object message)
    {
        IExternalSystem? exclude = receivingFrom.Value;
        foreach (IExternalSystem system in systems)
        {
            if (ReferenceEquals(system, exclude)) { continue; }
            await system.Send(message);
        }
    }
}
