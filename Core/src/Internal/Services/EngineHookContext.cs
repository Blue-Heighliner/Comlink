namespace BlueHeighliner.Comlink.Services;

/// <summary>Shared <see cref="IEngineHookContext"/> implementation for <see cref="UserConnectionHookContext"/> and <see cref="MessageReceivedHookContext"/>.</summary>
internal abstract class EngineHookContextBase : IEngineHookContext
{
    /// <summary>Initializes the shared state of a new hook context.</summary>
    /// <param name="currentUser">This instance's own installed user.</param>
    /// <param name="userNames">Every known user name in the messaging system.</param>
    /// <param name="userGroups">Every defined group as a map of group name to member names, for resolving each <see cref="Users"/> entry's direct memberships.</param>
    /// <param name="isConnected">Answers <see cref="IsConnected"/> for a user name.</param>
    /// <param name="engineController">Validates <see cref="SendMessage"/>/<see cref="SendPacket"/> against the configured message/packet type.</param>
    /// <param name="messageRouting">Routes a <see cref="SendMessage"/> call on <see cref="CurrentUser"/>'s behalf.</param>
    /// <param name="peerService">Sends a <see cref="SendPacket"/> call directly to each resolved recipient.</param>
    /// <param name="logger">Logs a failed <see cref="SendMessage"/>/<see cref="SendPacket"/>, since both are fire-and-forget and nothing else observes their outcome.</param>
    protected EngineHookContextBase(
        UserInfo currentUser,
        IReadOnlyList<string> userNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups,
        Func<string, bool> isConnected,
        IEngineController engineController,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        ILogger logger)
    {
        CurrentUser = currentUser;
        this.userNames = userNames;
        this.userGroups = userGroups;
        this.isConnected = isConnected;
        this.engineController = engineController;
        this.messageRouting = messageRouting;
        this.peerService = peerService;
        this.logger = logger;
    }

    private readonly IReadOnlyList<string> userNames;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups;
    private readonly Func<string, bool> isConnected;
    private readonly IEngineController engineController;
    private readonly IMessageRoutingService messageRouting;
    private readonly IPeerService peerService;
    private readonly ILogger logger;

    /// <inheritdoc />
    public UserInfo CurrentUser { get; }

    /// <inheritdoc />
    public IEnumerable<UserInfo> Users => userNames.Select(name => new UserInfo
    {
        Name = name,
        Code = string.Empty,
        Groups = [.. userGroups.Where(group => group.Value.Contains(name, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)]
    });

    /// <inheritdoc />
    public IEnumerable<UserInfo> ConnectedUsers => Users.Where(user => IsConnected(user.Name));

    /// <inheritdoc />
    public bool IsConnected(string userName) => isConnected(userName);

    /// <inheritdoc />
    public void SendMessage(object message)
    {
        if (message.GetType() != engineController.MessageType)
        {
            throw new ArgumentException($"Must be an instance of the configured message type ({engineController.MessageType}).", nameof(message));
        }

        _ = SendMessageInBackground(message);
    }

    /// <inheritdoc />
    public void SendPacket(object packet, params IEnumerable<string> userNames)
    {
        if (engineController.PacketType is not { } packetType)
        {
            throw new ArgumentException("Packetization is not enabled; state a packet type with Packets<TPacket>(...) to enable it.", nameof(packet));
        }
        if (packet.GetType() != packetType)
        {
            throw new ArgumentException($"Must be an instance of the configured packet type ({packetType}).", nameof(packet));
        }

        foreach (string userName in userNames)
        {
            _ = SendPacketInBackground(userName, packet);
        }
    }

    private async Task SendMessageInBackground(object message)
    {
        try { await messageRouting.RouteMessage(CurrentUser.Name, message, CancellationToken.None); }
        catch (Exception ex) { logger.LogError(ex, "A hook-originated message send failed"); }
    }

    private async Task SendPacketInBackground(string userName, object packet)
    {
        try { await peerService.SendPacket(userName, packet); }
        catch (Exception ex) { logger.LogError(ex, "A hook-originated packet send to {UserName} failed", userName); }
    }
}

/// <inheritdoc cref="IUserConnectionHookContext" />
internal sealed class UserConnectionHookContext : EngineHookContextBase, IUserConnectionHookContext
{
    /// <summary>Initializes a new <see cref="UserConnectionHookContext"/>.</summary>
    /// <param name="currentUser">This instance's own installed user.</param>
    /// <param name="userNames">Every known user name in the messaging system.</param>
    /// <param name="userGroups">Every defined group as a map of group name to member names, for resolving each <see cref="EngineHookContextBase.Users"/> entry's direct memberships.</param>
    /// <param name="isConnected">Answers <see cref="EngineHookContextBase.IsConnected"/> for a user name.</param>
    /// <param name="targetUser">The user that connected or disconnected.</param>
    /// <param name="engineController">Validates <see cref="EngineHookContextBase.SendMessage"/>/<see cref="EngineHookContextBase.SendPacket"/> against the configured message/packet type.</param>
    /// <param name="messageRouting">Routes a <see cref="EngineHookContextBase.SendMessage"/> call on <see cref="EngineHookContextBase.CurrentUser"/>'s behalf.</param>
    /// <param name="peerService">Sends a <see cref="EngineHookContextBase.SendPacket"/> call directly to each resolved recipient.</param>
    /// <param name="logger">Logs a failed send, since both <see cref="EngineHookContextBase.SendMessage"/> and <see cref="EngineHookContextBase.SendPacket"/> are fire-and-forget.</param>
    public UserConnectionHookContext(
        UserInfo currentUser,
        IReadOnlyList<string> userNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups,
        Func<string, bool> isConnected,
        string targetUser,
        IEngineController engineController,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        ILogger logger)
        : base(currentUser, userNames, userGroups, isConnected, engineController, messageRouting, peerService, logger)
    {
        TargetUser = targetUser;
    }

    /// <inheritdoc />
    public string TargetUser { get; }
}

/// <inheritdoc cref="IMessageReceivedHookContext" />
internal sealed class MessageReceivedHookContext : EngineHookContextBase, IMessageReceivedHookContext
{
    /// <summary>Initializes a new <see cref="MessageReceivedHookContext"/>.</summary>
    /// <param name="currentUser">This instance's own installed user.</param>
    /// <param name="userNames">Every known user name in the messaging system.</param>
    /// <param name="userGroups">Every defined group as a map of group name to member names, for resolving each <see cref="EngineHookContextBase.Users"/> entry's direct memberships.</param>
    /// <param name="isConnected">Answers <see cref="EngineHookContextBase.IsConnected"/> for a user name.</param>
    /// <param name="message">The message that was received (an instance of the configured message type).</param>
    /// <param name="engineController">Validates <see cref="EngineHookContextBase.SendMessage"/>/<see cref="EngineHookContextBase.SendPacket"/> against the configured message/packet type.</param>
    /// <param name="messageRouting">Routes a <see cref="EngineHookContextBase.SendMessage"/> call on <see cref="EngineHookContextBase.CurrentUser"/>'s behalf.</param>
    /// <param name="peerService">Sends a <see cref="EngineHookContextBase.SendPacket"/> call directly to each resolved recipient.</param>
    /// <param name="logger">Logs a failed send, since both <see cref="EngineHookContextBase.SendMessage"/> and <see cref="EngineHookContextBase.SendPacket"/> are fire-and-forget.</param>
    public MessageReceivedHookContext(
        UserInfo currentUser,
        IReadOnlyList<string> userNames,
        IReadOnlyDictionary<string, IReadOnlyList<string>> userGroups,
        Func<string, bool> isConnected,
        object message,
        IEngineController engineController,
        IMessageRoutingService messageRouting,
        IPeerService peerService,
        ILogger logger)
        : base(currentUser, userNames, userGroups, isConnected, engineController, messageRouting, peerService, logger)
    {
        Message = message;
    }

    /// <inheritdoc />
    public object Message { get; }
}
