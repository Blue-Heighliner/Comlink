namespace BlueHeighliner.Comlink;

/// <summary>What every context handed to the host's <see cref="INetworkProcessor{TFrame, TPriority, TLevel, TAspect}"/> is built on: one snapshot of the engine, and the environment the processor acts on, with the host's frame type.</summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
/// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
/// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
internal abstract class NetworkEngineContext<TFrame, TPriority, TLevel, TAspect> : INetworkContext<TFrame, TPriority, TLevel, TAspect> where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>Initializes the shared state of a new network context, taking the snapshot of the engine now.</summary>
    /// <param name="environment">What the processor acts on.</param>
    protected NetworkEngineContext(INetworkEnvironment environment)
    {
        this.environment = environment;
        engine = environment.CreateEngineContext();
    }

    private readonly INetworkEnvironment environment;
    private readonly IEngineContext engine;

    /// <inheritdoc />
    /// <summary>Returns everything known about <paramref name="userName"/>: the user the network configuration lists, or just the name for anyone else, such as an external system.</summary>
    /// <param name="userName">The user's name.</param>
    protected UserInfo GetUser(string userName) => engine.Users.GetValueOrDefault(userName) ?? new() { Name = userName };

    /// <inheritdoc />
    public UserInfo CurrentUser => engine.CurrentUser;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> Users => engine.Users;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UserInfo> ConnectedUsers => engine.ConnectedUsers;

    /// <inheritdoc />
    /// <inheritdoc />
    public IReadOnlyList<string> GetGroupMembers(string groupName) => engine.GetGroupMembers(groupName);

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
    public Task SendToExternalSystems(TFrame frame) => environment.SendToExternalSystems(frame);

    /// <inheritdoc />
    public Task StoreMessage(Message<TPriority, TLevel, TAspect> message) => environment.StoreMessage(message.ToUntyped());

    /// <inheritdoc />
    public async Task<IReadOnlyList<Message<TPriority, TLevel, TAspect>>> FindStoredMessages(RetrievalCriteria criteria) => [.. (await environment.FindStoredMessages(criteria)).Select(message => message.ToTyped<TPriority, TLevel, TAspect>())];

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(TLevel? minimumLevel, out IReadOnlySet<string> excluded, params IEnumerable<string> targets)
    {
        HashSet<string> destinations = [];
        HashSet<string> left = [];
        foreach (string target in targets)
        {
            IReadOnlyList<string> members = engine.GetGroupMembers(target);
            foreach (string user in members.Count > 0 ? members : [target])
            {
                if (destinations.Contains(user) || left.Contains(user))
                {
                    continue;
                }

                if (minimumLevel is { } level && !environment.IsAtLeast(user, level))
                {
                    left.Add(user);
                }
                else
                {
                    destinations.Add(user);
                }
            }
        }

        excluded = left;
        return destinations;
    }

    /// <inheritdoc />
    public IReadOnlySet<string> GetDestinations(Message<TPriority, TLevel, TAspect> message, out IReadOnlySet<string> excluded)
        => GetDestinations(message.MessageLevel, out excluded, message.Addresses.Where(address => address.Type is not AddressType.External).Select(address => address.UserName));

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAutoForwardTargets(string controllerName) => [.. (await environment.GetAutoForwardTargets(controllerName)).Where(target => target != CurrentUser.Name)];
}
