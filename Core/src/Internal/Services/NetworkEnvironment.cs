namespace BlueHeighliner.Comlink;

/// <summary>What the engine lets the host's frame handler do, with frames as plain objects since the engine does not know the host's frame type at compile time. A <see cref="EngineFrameHandler{TFrame, TPriority, TLevel, TAspect}"/> presents it to the handler with the frame type.</summary>
internal interface INetworkEnvironment
{
    /// <summary>Creates a snapshot of the engine as it is now.</summary>
    /// <exception cref="InvalidOperationException">No user is installed, which should never happen where a handler runs.</exception>
    IEngineContext CreateEngineContext();

    /// <summary>Sends <paramref name="frame"/> to <paramref name="userName"/> over the connection identified as them.</summary>
    /// <param name="userName">The user to send it to.</param>
    /// <param name="priority">A configured priority.</param>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <exception cref="ArgumentException"><paramref name="priority"/> is not a configured priority.</exception>
    Task<bool> Send(string userName, Enum priority, object frame);

    /// <summary>Records <paramref name="message"/> as received.</summary>
    /// <param name="message">The message.</param>
    /// <exception cref="ArgumentException">The message's priority, message level or message aspect is not a configured one.</exception>
    Task ReceiveMessage(Message message);

    /// <summary>Changes a destination's delivery status on a sent message.</summary>
    /// <param name="messageId">The identifier of the sent message.</param>
    /// <param name="userName">The destination.</param>
    /// <param name="status">The new status.</param>
    Task SetSentStatus(string messageId, string userName, DestinationStatus status);

    /// <summary>Changes the status of a received message to <see cref="DestinationStatus.Received"/> or <see cref="DestinationStatus.Read"/>.</summary>
    /// <param name="messageId">The identifier of the received message.</param>
    /// <param name="status">The new status.</param>
    Task SetReceivedStatus(string messageId, DestinationStatus status);

    /// <summary>Sets the network indicator online or offline.</summary>
    /// <param name="isOnline"><see langword="true"/> for online.</param>
    void SetNetworkIndicator(bool isOnline);

    /// <summary>Returns whether the user's own message level ranks at or above <paramref name="level"/>. Always <see langword="true"/> when message levels are not in use; a user with no level of their own runs at the lowest.</summary>
    /// <param name="userName">The user.</param>
    /// <param name="level">The level to compare with.</param>
    /// <exception cref="ArgumentException"><paramref name="level"/> is not one of the configured message levels.</exception>
    bool IsAtLeast(string userName, Enum level);

    /// <summary>Sends <paramref name="frame"/> to every connected interface.</summary>
    /// <param name="priority">A configured priority.</param>
    /// <param name="frame">An instance of the configured frame type.</param>
    /// <exception cref="ArgumentException"><paramref name="priority"/> is not a configured priority.</exception>
    Task SendInterface(Enum priority, object frame);

    /// <summary>Sends <paramref name="frame"/> to every external system.</summary>
    /// <param name="frame">An instance of the configured frame type.</param>
    Task SendToExternalSystems(object frame);

    /// <summary>Keeps a copy of <paramref name="message"/> for retrievals, unless one with its identifier is already kept.</summary>
    /// <param name="message">The message.</param>
    Task StoreMessage(Message message);

    /// <summary>Finds the kept messages that fit <paramref name="criteria"/>.</summary>
    /// <param name="criteria">What they must fit.</param>
    Task<IReadOnlyList<Message>> FindStoredMessages(RetrievalCriteria criteria);

    /// <summary>Gets the target list of an auto forwarder for the current user.</summary>
    /// <param name="controllerName">The controller's name.</param>
    Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName);
}

/// <inheritdoc cref="INetworkEnvironment" />
internal sealed class NetworkEnvironment(
    IServiceProvider services,
    IEngineController engineController,
    IEngineContextFactory contexts,
    IEntryService entryService,
    IMessageEvents events,
    IMessageStorageService storage,
    IAutoForwardTargetsRepository autoForwardTargets,
    INetworkIndicator indicator,
    ILoggerFactory loggerFactory) : INetworkEnvironment
{
    private readonly ILogger logger = loggerFactory.CreateLogger(LogCategories.App);

    /// <inheritdoc />
    public IEngineContext CreateEngineContext() => contexts.Create();

    /// <inheritdoc />
    public async Task<bool> Send(string userName, Enum priority, object frame)
        => await services.GetRequiredService<IPeerService>().Send(userName, frame, engineController.SendPriority(priority));

    /// <inheritdoc />
    public async Task ReceiveMessage(Message message)
    {
        _ = engineController.ToData(message);
        logger.Record(LogEvents.MessageReceived, "{MessageId} received from {FromUser}", message.Id, message.FromUser);
        await events.RaiseMessageReceived(message);
    }

    /// <inheritdoc />
    public async Task SetSentStatus(string messageId, string userName, DestinationStatus status)
    {
        MessageEntity? entity = await entryService.UpdateDeliveryStatus(messageId, userName, status);
        if (entity is null)
        {
            return;
        }

        // Reports the status as stored rather than as raised, since a late, out-of-order event may have been ignored.
        DestinationStatus effective = entity.DeliveryStatuses.FirstOrDefault(d => string.Equals(d.UserName, userName, StringComparison.OrdinalIgnoreCase))?.Status ?? status;
        logger.Record(LogEvents.DeliveryStatusChanged, "{MessageId} status for {User}: {Status}", messageId, userName, effective);
        await events.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, UserName = userName, Status = effective, OverallStatus = entity.OverallStatus });
    }

    /// <inheritdoc />
    public async Task SetReceivedStatus(string messageId, DestinationStatus status)
    {
        if (status is not DestinationStatus.Read)
        {
            return;
        }

        if (await entryService.MarkMessageRead(messageId) is null)
        {
            return;
        }

        await events.RaiseDeliveryStatusChanged(new DeliveryStatusChangedEvent { MessageId = messageId, Status = DestinationStatus.Read, OverallStatus = DestinationStatus.Read });
    }

    /// <inheritdoc />
    public void SetNetworkIndicator(bool isOnline) => indicator.Set(isOnline);

    /// <inheritdoc />
    public bool IsAtLeast(string userName, Enum level)
        => engineController.MessageLevels.Count == 0
            || engineController.MessageLevels.GetRank(engineController.GetUserMessageLevel(userName)) >= engineController.MessageLevels.GetRank(engineController.GetMessageLevelName(level));

    /// <inheritdoc />
    /// <inheritdoc />
    public Task SendInterface(Enum priority, object frame) => services.GetRequiredService<IInterfaceService>().Send(priority, frame);

    /// <inheritdoc />
    public Task SendToExternalSystems(object frame) => services.GetRequiredService<IExternalSystemsService>().Send(frame);

    /// <inheritdoc />
    public Task StoreMessage(Message message) => storage.Store(message);

    /// <inheritdoc />
    public Task<IReadOnlyList<Message>> FindStoredMessages(RetrievalCriteria criteria) => storage.Find(criteria);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName) => [.. (await autoForwardTargets.Get(controllerName))?.Targets ?? []];
}
