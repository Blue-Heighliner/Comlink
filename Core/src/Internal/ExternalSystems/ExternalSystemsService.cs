namespace BlueHeighliner.Comlink;

/// <summary>
/// Coordinates every configured <see cref="IExternalSystem"/> (see <see cref="IEngineController.ExternalSystems"/>): runs each one's own connect/poll/disconnect lifecycle, hands every frame an external system
/// delivers to the host's network processor (see <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}.OnReceived"/>), and sends the frames the processor asks it to (see <see cref="INetworkContext{TFrame, TPriority, TLevel, TAspect}.SendToExternalSystems"/>).
/// What a frame from an external system means, and whether it goes anywhere, is the processor's: the engine does not treat it as a received message or mirror it to anything. See <c>Docs/Components/ExternalSystems.md</c>.
/// </summary>
internal interface IExternalSystemsService
{
    /// <summary>Starts every configured external system's connect/poll/disconnect lifecycle and wires up what they deliver. Blocks until <paramref name="cancellation"/> is cancelled.</summary>
    Task Start(CancellationToken cancellation);

    /// <summary>Sends <paramref name="frame"/> to every external system, each of which takes only the frames it handles.</summary>
    /// <param name="frame">An instance of the configured frame type.</param>
    Task Send(object frame);
}

/// <inheritdoc cref="IExternalSystemsService" />
internal sealed class ExternalSystemsService : IExternalSystemsService
{
    /// <summary>Initializes a new <see cref="ExternalSystemsService"/>, resolving the configured external systems once.</summary>
    public ExternalSystemsService(IEngineController engineController, INetworkProcessing processing, ILoggerFactory loggerFactory)
    {
        this.processing = processing;
        this.engineController = engineController;
        systems = engineController.ExternalSystems;
        logger = loggerFactory.CreateLogger(LogCategories.App);
    }

    private readonly INetworkProcessing processing;
    private readonly IEngineController engineController;
    private readonly IReadOnlyList<IExternalSystem> systems;
    private readonly ILogger logger;
    private readonly HashSet<IExternalSystem> attached = [];

    /// <inheritdoc />
    public async Task Start(CancellationToken cancellation)
    {
        if (systems.Count == 0)
        {
            return;
        }

        foreach (IExternalSystem system in systems)
        {
            system.AttachLogger(logger);
            if (attached.Add(system))
            {
                system.MessageReceived += message => OnExternalSystemMessageReceived(system, message);
            }
        }

        await Task.WhenAll(systems.Select(system => RunSystem(system, cancellation)));
    }

    /// <inheritdoc />
    public async Task Send(object frame)
    {
        foreach (IExternalSystem system in systems)
        {
            await system.Send(frame);
        }
    }

    private async Task RunSystem(IExternalSystem system, CancellationToken cancellation)
    {
        try { await system.Start(cancellation); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.Record(LogEvents.ExternalSystemStoppedUnexpectedly, ex, "External system {Name} stopped unexpectedly", system.Name);
            logger.Record(LogEvents.ExternalSystemStopped, "External system {Name} stopped working", system.Name);
        }
    }

    private Task OnExternalSystemMessageReceived(IExternalSystem source, object message)
    {
        if (message.GetType() == engineController.FrameType)
        {
            processing.Received(message, FrameOrigin.ExternalSystem, source.Name);
        }

        return Task.CompletedTask;
    }
}
