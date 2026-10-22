namespace BlueHeighliner.Comlink;

/// <inheritdoc cref="INetwork{TFrame, TPriority, TLevel, TAspect}" />
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its aspects.</typeparam>
/// <param name="environment">What the engine lets a handler do, with frames as plain objects.</param>
/// <param name="users">Says who the current user is.</param>
internal sealed class Network<TFrame, TPriority, TLevel, TAspect>(INetworkEnvironment environment, IUserDirectory users) : INetwork<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public Task<bool> Send(string userName, TPriority priority, TFrame frame) => environment.Send(userName, priority, frame);

    /// <inheritdoc />
    public Task ReceiveMessage(Message<TPriority, TLevel, TAspect> message) => environment.ReceiveMessage(message.ToUntyped());

    /// <inheritdoc />
    public Task SetSentStatus(string messageId, string userName, DestinationStatus status) => environment.SetSentStatus(messageId, userName, status);

    /// <inheritdoc />
    public async Task SetSentStatus(string messageId, IEnumerable<string> userNames, DestinationStatus status)
    {
        foreach (string userName in userNames)
        {
            await environment.SetSentStatus(messageId, userName, status);
        }
    }

    /// <inheritdoc />
    public Task SetReceivedStatus(string messageId, DestinationStatus status) => environment.SetReceivedStatus(messageId, status);

    /// <inheritdoc />
    public void SetNetworkIndicator(bool isOnline) => environment.SetNetworkIndicator(isOnline);

    /// <inheritdoc />
    public Task SendInterface(TPriority priority, TFrame frame) => environment.SendInterface(priority, frame);

    /// <inheritdoc />
    public Task SendToExternalSystems(TFrame frame) => environment.SendToExternalSystems(frame);

    /// <inheritdoc />
    public Task StoreMessage(Message<TPriority, TLevel, TAspect> message) => environment.StoreMessage(message.ToUntyped());

    /// <inheritdoc />
    public async Task<IReadOnlyList<Message<TPriority, TLevel, TAspect>>> FindStoredMessages(RetrievalCriteria criteria) => [.. (await environment.FindStoredMessages(criteria)).Select(message => message.ToTyped<TPriority, TLevel, TAspect>())];

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(TLevel? minimumLevel, out IReadOnlySet<string> excluded, params IEnumerable<string> targets)
        => environment.GetDestinations(minimumLevel is { } level ? (Enum)level : null, targets, out excluded);

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded)
        => GetDestinations(message.Level, out excluded, message.Addresses.Where(address => address.Type is not AddressType.External).Select(address => address.UserName));

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName)
    {
        string current = users.CurrentUser.Name;
        return [.. (await environment.GetAutoForwardTargets(controllerName)).Where(target => target != current)];
    }
}
