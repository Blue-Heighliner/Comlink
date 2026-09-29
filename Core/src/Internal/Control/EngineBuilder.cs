namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Implements <see cref="IEngineBuilder"/>: collects everything a host states in <see cref="IEngineConfiguration.Configure"/>.
/// Nothing is interpreted here; <see cref="EngineController"/> reads the collected state and applies the defaults for
/// whatever was left unstated.
/// </summary>
internal sealed class EngineBuilder : IEngineBuilder, IAsyncDisposable
{
    private readonly Dictionary<string, IReadOnlyList<string>> groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> userData = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SecurityLevel> securityLevels = [];
    private readonly Dictionary<string, string> userSecurityLevels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ServerUserConfig> servers = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> users = [];
    private readonly List<MessagePriorityOption> priorities = [];
    private readonly List<TagPriorityBlock> blocked = [];
    private readonly Dictionary<AddressType, string> addressTypeLabels = [];
    private readonly List<ConnectionPoint> outgoingPoints = [];
    private readonly List<IExternalSystem> externalSystems = [];
    private readonly List<Action<IUserConnectionHookContext>> userConnectedHooks = [];
    private readonly List<Action<IUserConnectionHookContext>> userDisconnectedHooks = [];
    private readonly List<Action<IMessageReceivedHookContext>> messageReceivedHooks = [];
    private ServiceProvider? bootstrap;

    /// <summary>The message mapping, or <see langword="null"/> until <see cref="Message{TMessage}"/> is called.</summary>
    public MessageMap? MessageMap { get; private set; }
    /// <summary>The packet mapping, or <see langword="null"/> while packetization is off.</summary>
    public PacketMap? PacketMap { get; private set; }
    /// <summary>The application name, if stated.</summary>
    public string? AppNameValue { get; private set; }
    /// <summary>The application version, if stated.</summary>
    public string? AppVersionValue { get; private set; }
    /// <summary>The application data path, if stated.</summary>
    public string? DataPathValue { get; private set; }
    /// <summary>Whether kiosk mode is on.</summary>
    public bool IsKioskMode { get; private set; }
    /// <summary>The home text, if stated.</summary>
    public string? HomeTextValue { get; private set; }
    /// <summary>The window icon, if stated.</summary>
    public Uri? WindowIconValue { get; private set; }
    /// <summary>The debug user name, if stated.</summary>
    public string? DebugUserValue { get; private set; }
    /// <summary>How installation codes resolve, if stated.</summary>
    public Func<string, UserInfo?>? UserCodeResolver { get; private set; }
    /// <summary>The user names added to the directory.</summary>
    public IReadOnlyList<string> UserNames => users;
    /// <summary>The user groups.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups => groups;
    /// <summary>The data attached to users by name.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, string>> UserDataByName => userData;
    /// <summary>How the data attached to any user is looked up, if stated.</summary>
    public Func<string, IReadOnlyDictionary<string, string>>? UserDataLookup { get; private set; }
    /// <summary>The configured security levels, in ascending order; empty when none were stated.</summary>
    public IReadOnlyList<SecurityLevel> SecurityLevelValues => securityLevels;
    /// <summary>The security levels assigned to users by name.</summary>
    public IReadOnlyDictionary<string, string> UserSecurityLevelsByName => userSecurityLevels;
    /// <summary>How the security level for any user name is looked up, if stated.</summary>
    public Func<string, string>? UserSecurityLevelLookup { get; private set; }
    /// <summary>The peer listener port, if stated.</summary>
    public int? PeerPortValue { get; private set; }
    /// <summary>The interface listener port, if stated.</summary>
    public int? InterfacePortValue { get; private set; }
    /// <summary>The alert label, if stated.</summary>
    public string? AlertLabelValue { get; private set; }
    /// <summary>The alarm duration, if stated.</summary>
    public TimeSpan? AlarmDurationValue { get; private set; }
    /// <summary>Whether quick confirmation is on, if stated.</summary>
    public bool? QuickConfirmationValue { get; private set; }
    /// <summary>Whether composing alerts is on, if stated.</summary>
    public bool? ComposeAlertsValue { get; private set; }
    /// <summary>The selectable priorities, empty when none were stated.</summary>
    public IReadOnlyList<MessagePriorityOption> PriorityOptions => priorities;
    /// <summary>Whether tags are shown, if stated.</summary>
    public bool? TagsEnabledValue { get; private set; }
    /// <summary>The tag label, if stated.</summary>
    public string? TagLabelValue { get; private set; }
    /// <summary>The blocked tag and priority combinations.</summary>
    public IReadOnlyList<TagPriorityBlock> BlockedCombinations => blocked;
    /// <summary>The overridden address type display labels, by address type.</summary>
    public IReadOnlyDictionary<AddressType, string> AddressTypeLabels => addressTypeLabels;
    /// <summary>Whether printing received messages starts on, if stated.</summary>
    public bool? PrintReceivedValue { get; private set; }
    /// <summary>How many copies of a received message print, if stated.</summary>
    public Func<object, int>? PrintCountValue { get; private set; }
    /// <summary>Which folders allow deleting, if stated.</summary>
    public Func<FolderType, bool>? CanDeleteValue { get; private set; }
    /// <summary>How a user name maps to a certificate name, if stated.</summary>
    public Func<string, string>? CertificateNameValue { get; private set; }
    /// <summary>The trusted authority certificate name, if stated.</summary>
    public string? TrustedAuthorityValue { get; private set; }
    /// <summary>How the MSMT peer options are built, if stated.</summary>
    public Func<MsmtSessionPeerOptions>? ConnectionOptionsValue { get; private set; }
    /// <summary>The networking role, if stated.</summary>
    public NodeRole? RoleValue { get; private set; }
    /// <summary>The points this node connects out to.</summary>
    public IReadOnlyList<ConnectionPoint> OutgoingPoints => outgoingPoints;
    /// <summary>The server topology.</summary>
    public IReadOnlyDictionary<string, ServerUserConfig> ServerTopology => servers;
    /// <summary>How connections are identified, if stated.</summary>
    public Func<ConnectionInfo, UserIdentity?>? IdentifyValue { get; private set; }
    /// <summary>The connection message type, if stated.</summary>
    public Type? ConnectionMessageType { get; private set; }
    /// <summary>Builds the connection message, if stated.</summary>
    public Func<ConnectionInfo, object?>? ConnectionMessageFactory { get; private set; }
    /// <summary>The connection response type, if stated.</summary>
    public Type? ConnectionResponseType { get; private set; }
    /// <summary>Builds the connection response, if stated.</summary>
    public Func<ConnectionInfo, object?>? ConnectionResponseFactory { get; private set; }
    /// <summary>The connection message serializer, if stated.</summary>
    public INetworkSerializer? ConnectionSerializerValue { get; private set; }
    /// <summary>Whether the config file is read.</summary>
    public bool IsConfigFileEnabled { get; private set; }
    /// <summary>The external systems.</summary>
    public IReadOnlyList<IExternalSystem> ExternalSystems => externalSystems;
    /// <summary>The designated upstream hub, if any.</summary>
    public IExternalSystem? ExternalServerValue { get; private set; }
    /// <summary>The hooks run when a user comes online.</summary>
    public IReadOnlyList<Action<IUserConnectionHookContext>> UserConnectedHooks => userConnectedHooks;
    /// <summary>The hooks run when a user goes offline.</summary>
    public IReadOnlyList<Action<IUserConnectionHookContext>> UserDisconnectedHooks => userDisconnectedHooks;
    /// <summary>The hooks run when a message is received.</summary>
    public IReadOnlyList<Action<IMessageReceivedHookContext>> MessageReceivedHooks => messageReceivedHooks;

    /// <summary>
    /// Constructs <typeparamref name="TConfiguration"/> from a bootstrap container, so it can take dependencies, then
    /// runs it against a new builder and checks the result. The bootstrap container holds logging plus whatever
    /// <paramref name="configureServices"/> registers, and lives until the returned builder is disposed, since the
    /// configuration may have handed the engine functions that use what it was given.
    /// </summary>
    /// <typeparam name="TConfiguration">The host's configuration type.</typeparam>
    /// <param name="configureServices">Registers the services the configuration depends on.</param>
    /// <exception cref="InvalidOperationException">The configuration cannot be constructed, or is incomplete or contradicts itself.</exception>
    public static EngineBuilder Build<TConfiguration>(Action<IServiceCollection>? configureServices) where TConfiguration : class, IEngineConfiguration
    {
        ServiceCollection services = new();
        services.AddLogging();
        configureServices?.Invoke(services);
        services.AddSingleton<TConfiguration>();
        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            EngineBuilder builder = Build(provider.GetRequiredService<TConfiguration>());
            builder.bootstrap = provider;
            return builder;
        }
        catch
        {
            provider.Dispose();
            throw;
        }
    }

    /// <summary>Runs <paramref name="configuration"/> against a new builder and checks the result.</summary>
    /// <exception cref="InvalidOperationException">The configuration is incomplete or contradicts itself.</exception>
    public static EngineBuilder Build(IEngineConfiguration configuration)
    {
        EngineBuilder builder = new();
        configuration.Configure(builder);
        builder.Validate();
        return builder;
    }

    /// <inheritdoc />
    public IEngineBuilder Message<TMessage>(Action<IMessageBuilder<TMessage>> map) where TMessage : class, new()
    {
        MessageBuilder<TMessage> builder = new();
        map(builder);
        MessageMap = builder.Build();
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Packets<TPacket>(Action<IPacketBuilder<TPacket>> map) where TPacket : class, new()
    {
        PacketBuilder<TPacket> builder = new();
        map(builder);
        PacketMap = builder.Build();
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder AppName(string name)
    {
        AppNameValue = name;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder AppVersion(string version)
    {
        AppVersionValue = version;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder DataPath(string path)
    {
        DataPathValue = path;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder KioskMode(bool enabled = true)
    {
        IsKioskMode = enabled;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder HomeText(string text)
    {
        HomeTextValue = text;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder WindowIcon(Uri uri)
    {
        WindowIconValue = uri;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder DebugUser(string userName)
    {
        DebugUserValue = userName;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder UserCodes(Func<string, UserInfo?> resolve)
    {
        UserCodeResolver = resolve;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Users(params string[] names)
    {
        users.AddRange(names);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Group(string name, params string[] members)
    {
        groups[name] = members;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder UserData(string userName, IReadOnlyDictionary<string, string> data)
    {
        if (!userData.TryGetValue(userName, out Dictionary<string, string>? existing))
        {
            existing = [];
            userData[userName] = existing;
        }

        foreach ((string key, string value) in data) { existing[key] = value; }
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder UserData(Func<string, IReadOnlyDictionary<string, string>> lookup)
    {
        UserDataLookup = lookup;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder SecurityLevels(params (string Name, string Color)[] levels)
    {
        securityLevels.Clear();
        securityLevels.AddRange(levels.Select(level => new SecurityLevel { Name = level.Name, Color = level.Color }));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder UserSecurityLevel(string userName, string levelName)
    {
        userSecurityLevels[userName] = levelName;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder UserSecurityLevel(Func<string, string> lookup)
    {
        UserSecurityLevelLookup = lookup;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder PeerPort(int port)
    {
        PeerPortValue = port;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder InterfacePort(int port)
    {
        InterfacePortValue = port;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder AlertLabel(string label)
    {
        AlertLabelValue = label;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder AlarmDuration(TimeSpan duration)
    {
        AlarmDurationValue = duration;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder QuickConfirmation(bool enabled = true)
    {
        QuickConfirmationValue = enabled;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ComposeAlerts(bool enabled = true)
    {
        ComposeAlertsValue = enabled;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Priorities(params (string Name, int Value)[] priorities)
    {
        this.priorities.Clear();
        this.priorities.AddRange(priorities.Select(priority => new MessagePriorityOption { Name = priority.Name, Value = priority.Value }));
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Tags(bool enabled = true, string? label = null)
    {
        TagsEnabledValue = enabled;
        if (label is not null) { TagLabelValue = label; }
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder BlockTag(string? tag, int? priority)
    {
        blocked.Add(new TagPriorityBlock { Tag = tag, Priority = priority });
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder AddressTypeLabel(AddressType type, string label)
    {
        addressTypeLabels[type] = label;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder PrintReceived(bool enabledByDefault = true)
    {
        PrintReceivedValue = enabledByDefault;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder PrintCount<TMessage>(Func<TMessage, int> copies) where TMessage : class
    {
        PrintCountValue = message => copies((TMessage)message);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder CanDelete(Func<FolderType, bool> allowed)
    {
        CanDeleteValue = allowed;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder CertificateName(Func<string, string> name)
    {
        CertificateNameValue = name;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder TrustedAuthority(string certificateName)
    {
        TrustedAuthorityValue = certificateName;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ConnectionOptions(Func<MsmtSessionPeerOptions> options)
    {
        ConnectionOptionsValue = options;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Role(NodeRole role)
    {
        RoleValue = role;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder OutgoingPoint(ConnectionPoint point)
    {
        outgoingPoints.Add(point);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Server(string name, params string[] childClients)
    {
        servers[name] = new ServerUserConfig { ChildClients = childClients };
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder Identify(Func<ConnectionInfo, UserIdentity?> identify)
    {
        IdentifyValue = identify;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ConnectionMessage<TMessage>(Func<ConnectionInfo, TMessage?> create) where TMessage : class
    {
        ConnectionMessageType = typeof(TMessage);
        ConnectionMessageFactory = create;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ConnectionResponse<TResponse>(Func<ConnectionInfo, TResponse?> create) where TResponse : class
    {
        ConnectionResponseType = typeof(TResponse);
        ConnectionResponseFactory = create;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ConnectionSerializer(INetworkSerializer serializer)
    {
        ConnectionSerializerValue = serializer;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ConfigFile(bool enabled = true)
    {
        IsConfigFileEnabled = enabled;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ExternalSystem(IExternalSystem system)
    {
        if (!externalSystems.Contains(system)) { externalSystems.Add(system); }
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder ExternalServer(IExternalSystem system)
    {
        ExternalSystem(system);
        ExternalServerValue = system;
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder OnUserConnected(Action<IUserConnectionHookContext> hook)
    {
        userConnectedHooks.Add(hook);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder OnUserDisconnected(Action<IUserConnectionHookContext> hook)
    {
        userDisconnectedHooks.Add(hook);
        return this;
    }

    /// <inheritdoc />
    public IEngineBuilder OnMessageReceived(Action<IMessageReceivedHookContext> hook)
    {
        messageReceivedHooks.Add(hook);
        return this;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => bootstrap?.DisposeAsync() ?? ValueTask.CompletedTask;

    private void Validate()
    {
        if (MessageMap is null) { throw new InvalidOperationException("The engine configuration must state its message type with Message<TMessage>(...)."); }
    }
}
