namespace BlueHeighliner.Comlink;

/// <summary>
/// Engine-level decorator applying the network configuration file's node settings for the user this process runs as, over
/// whichever <see cref="IEngineController"/> is built from the host's <see cref="IEngineConfiguration"/> (an
/// <see cref="EngineController"/>): the identity certificate file, and the alert, tag and print
/// settings, field by field; every other member, including the entire message-format surface and everything
/// about users (which the wrapped <see cref="EngineController"/> reads from the same file), delegates straight to the wrapped provider.
/// Registered by <see cref="EngineExtensions.UseEngine"/>. The user is the one named on the command line, or else the installed
/// user, whose folder <see cref="AppDataPath"/> names even before the install state has been read.
/// </summary>
internal sealed class ConfiguredEngineController : IEngineController
{
    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">The network configuration providing the node settings.</param>
    /// <param name="currentUserProvider">Tracks the user name of the currently running instance, needed by <see cref="ConnectionOptions"/>.</param>
    public ConfiguredEngineController(IEngineController fallback, NetworkConfig config, ICurrentUserProvider currentUserProvider)
    {
        this.fallback = fallback;
        this.config = config;
        this.currentUserProvider = currentUserProvider;
    }

    private readonly IEngineController fallback;
    private readonly NetworkConfig config;
    private readonly ICurrentUserProvider currentUserProvider;

    private NetworkUserConfig? Current => config.Find(DebugUserName ?? currentUserProvider.UserName);

    /// <inheritdoc />
    public Type FrameType => fallback.FrameType;
    /// <inheritdoc />
    public IFrameSerializer FrameSerializer => fallback.FrameSerializer;
    /// <inheritdoc />
    public Type? PacketType => fallback.PacketType;
    /// <inheritdoc />
    public IPacketSerializer? PacketSerializer => fallback.PacketSerializer;
    /// <inheritdoc />
    public int MaxPayloadSize => fallback.MaxPayloadSize;
    /// <inheritdoc />
    public int PacketWindow => fallback.PacketWindow;

    /// <inheritdoc />
    public object CreateFrame() => fallback.CreateFrame();

    /// <inheritdoc />
    public bool HeartbeatsEnabled => fallback.HeartbeatsEnabled;
    /// <inheritdoc />
    public bool PacketHeartbeatsEnabled => fallback.PacketHeartbeatsEnabled;
    /// <inheritdoc />
    public object CreatePacketHeartbeat() => fallback.CreatePacketHeartbeat();
    /// <inheritdoc />
    public bool IsPacketHeartbeat(object packet) => fallback.IsPacketHeartbeat(packet);
    /// <inheritdoc />
    public int PacketHeartbeatPriority => fallback.PacketHeartbeatPriority;
    /// <inheritdoc />
    public TimeSpan HeartbeatInterval => fallback.HeartbeatInterval;
    /// <inheritdoc />
    public TimeSpan HeartbeatRetryInterval => fallback.HeartbeatRetryInterval;
    /// <inheritdoc />
    public object CreateHeartbeat() => fallback.CreateHeartbeat();
    /// <inheritdoc />
    public bool IsHeartbeat(object frame) => fallback.IsHeartbeat(frame);

    /// <inheritdoc />
    public int LowestPriority => fallback.LowestPriority;
    /// <inheritdoc />
    public int HighestPriority => fallback.HighestPriority;
    /// <inheritdoc />
    public Enum ResolvePriority(Enum? priority) => fallback.ResolvePriority(priority);
    /// <inheritdoc />
    public Enum RequirePriority(Enum? priority) => fallback.RequirePriority(priority);
    /// <inheritdoc />
    public void Validate() => fallback.Validate();
    /// <inheritdoc />
    public Enum PriorityOf(int? value) => fallback.PriorityOf(value);
    /// <inheritdoc />
    public int StoredPriority(Enum priority) => fallback.StoredPriority(priority);
    /// <inheritdoc />
    public string NameOf(Enum priority) => fallback.NameOf(priority);
    /// <inheritdoc />
    public int HeartbeatPriority => fallback.HeartbeatPriority;
    /// <inheritdoc />
    public int SendPriority(Enum? priority) => fallback.SendPriority(priority);
    /// <inheritdoc />
    public string GetMessageLevelName(Enum? level) => fallback.GetMessageLevelName(level);
    /// <inheritdoc />
    public object CreateFramePacket(object frame, int index, int count, int frameLength, ReadOnlyMemory<byte> payload) => fallback.CreateFramePacket(frame, index, count, frameLength, payload);
    /// <inheritdoc />
    public bool IsFramePacket(object packet) => fallback.IsFramePacket(packet);
    /// <inheritdoc />
    public string GetFrameId(object packet) => fallback.GetFrameId(packet);
    /// <inheritdoc />
    public int GetPacketIndex(object packet) => fallback.GetPacketIndex(packet);
    /// <inheritdoc />
    public int GetPacketCount(object packet) => fallback.GetPacketCount(packet);
    /// <inheritdoc />
    public int GetFrameLength(object packet) => fallback.GetFrameLength(packet);
    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPacketPayload(object packet) => fallback.GetPacketPayload(packet);

    /// <inheritdoc />
    public string AppName => fallback.AppName;
    /// <inheritdoc />
    public string AppVersion => fallback.AppVersion;
    /// <inheritdoc />
    public string AppDataRoot => fallback.AppDataRoot;
    /// <inheritdoc />
    public string AppDataPath => currentUserProvider.UserName is null && DebugUserName is { } debug && fallback.FindUserName(debug) is { } name ? Path.Combine(Path.GetDirectoryName(fallback.UserFilePath)!, name) : fallback.AppDataPath;
    /// <inheritdoc />
    public string UserFilePath => fallback.UserFilePath;
    /// <inheritdoc />
    public bool IsKioskMode => fallback.IsKioskMode;
    /// <inheritdoc />
    public string GetNetworkIndicatorLabel(bool isOnline) => fallback.GetNetworkIndicatorLabel(isOnline);
    /// <inheritdoc />
    public string GetNetworkIndicatorColor(bool isOnline) => fallback.GetNetworkIndicatorColor(isOnline);
    /// <inheritdoc />
    public LogFieldWidths LogWidths => fallback.LogWidths;
    /// <inheritdoc />
    public string LoggingFilePath => fallback.LoggingFilePath;
    /// <inheritdoc />
    public bool SeparateAlerts => fallback.SeparateAlerts;
    /// <inheritdoc />
    public string HomeText => fallback.HomeText;
    /// <inheritdoc />
    public string PriorityLabel => fallback.PriorityLabel;
    /// <inheritdoc />
    public string PriorityPluralLabel => fallback.PriorityPluralLabel;
    /// <inheritdoc />
    public string MessageLevelPluralLabel => fallback.MessageLevelPluralLabel;
    /// <inheritdoc />
    public string MessageLevelLabel => fallback.MessageLevelLabel;
    /// <inheritdoc />
    public string MessageAspectLabel => fallback.MessageAspectLabel;
    /// <inheritdoc />
    public string MessageAspectPluralLabel => fallback.MessageAspectPluralLabel;
    /// <inheritdoc />
    public IReadOnlyList<MessageAspect> MessageAspects => fallback.MessageAspects;
    /// <inheritdoc />
    public string GetMessageAspectName(Enum? aspect) => fallback.GetMessageAspectName(aspect);
    /// <inheritdoc />
    public string UserLabel => fallback.UserLabel;
    /// <inheritdoc />
    public string UserPluralLabel => fallback.UserPluralLabel;
    /// <inheritdoc />
    public string Rename(string label) => fallback.Rename(label);
    /// <inheritdoc />
    public string? WindowIconPath => fallback.WindowIconPath;

    /// <inheritdoc />
    public string? DebugUserName => config.User ?? fallback.DebugUserName;
    /// <inheritdoc />
    public IReadOnlyList<string> Users => fallback.Users;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups => fallback.UserGroups;
    /// <inheritdoc />
    public IReadOnlyList<string> GetGroupMembers(string groupName) => fallback.GetGroupMembers(groupName);

    /// <inheritdoc />
    public UserRole Role => fallback.Role;
    /// <inheritdoc />
    public IReadOnlyList<ConnectionPoint> OutgoingPoints => fallback.OutgoingPoints;
    /// <inheritdoc />
    public string? ParentUser => fallback.ParentUser;
    /// <inheritdoc />
    public IReadOnlyList<ConnectionPoint> ParentPoints => fallback.ParentPoints;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, ServerUserConfig> Servers => fallback.Servers;
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetUserData(string userName) => fallback.GetUserData(userName);

    /// <inheritdoc />
    public int PeerPort => fallback.PeerPort;
    /// <inheritdoc />
    public int InterfacePort => fallback.InterfacePort;

    /// <inheritdoc />
    public string AlertPluralLabel => fallback.AlertPluralLabel;
    /// <inheritdoc />
    public string TagPluralLabel => fallback.TagPluralLabel;
    /// <inheritdoc />
    public string AlertLabel => fallback.AlertLabel;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => fallback.AlarmSoundDuration;
    /// <inheritdoc />
    public TimeSpan DisconnectAlarmDuration => fallback.DisconnectAlarmDuration;
    /// <inheritdoc />
    public LineWidthRange? DraftLineWidth => fallback.DraftLineWidth;
    /// <inheritdoc />
    public TagRules DraftTagRules => fallback.DraftTagRules;
    /// <inheritdoc />
    public DraftDefaults DraftDefaults => fallback.DraftDefaults;
    /// <inheritdoc />
    public string? GetDraftHeader(DraftContent draft) => fallback.GetDraftHeader(draft);
    /// <inheritdoc />
    public bool IsAlert(DraftContent draft) => fallback.IsAlert(draft);
    /// <inheritdoc />
    public string NextMessageId(string? previous) => fallback.NextMessageId(previous);

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> Priorities => fallback.Priorities;
    /// <inheritdoc />
    public bool TagsEnabled => fallback.TagsEnabled;
    /// <inheritdoc />
    public string TagLabel => fallback.TagLabel;
    /// <inheritdoc />
    public bool IsDraftAllowed(IEngineContext context, Enum priority, Enum? level, Enum? aspect, string tag) => fallback.IsDraftAllowed(context, priority, level, aspect, tag);
    /// <inheritdoc />
    public IReadOnlyList<AddressTypeOption> AddressTypes => fallback.AddressTypes;
    /// <inheritdoc />
    public IReadOnlyList<MessageLevel> MessageLevels => fallback.MessageLevels;
    /// <inheritdoc />
    public string GetUserMessageLevel(string userName) => fallback.GetUserMessageLevel(userName);

    /// <inheritdoc />
    public bool PrintReceivedDefaultEnabled => fallback.PrintReceivedDefaultEnabled;
    /// <inheritdoc />
    public int GetPrintCount(Message message) => fallback.GetPrintCount(message);

    /// <inheritdoc />
    public bool CanDelete(FolderType folderType) => fallback.CanDelete(folderType);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The network's <c>CertificateStore</c> or <c>AuthorityCertificate</c> is not set, or there is no current user to load a certificate for.</exception>
    public MsmtSessionPeerOptions ConnectionOptions
    {
        get
        {
            string? store = config.CertificateStore;
            string? authorityFile = config.GetAuthorityCertificatePath();
            if (store is null || authorityFile is null)
            {
                throw new InvalidOperationException("Peer authentication requires the network's CertificateStore and AuthorityCertificate to be set.");
            }

            string userName = currentUserProvider.UserName ?? DebugUserName?.ToUpperInvariant() ?? throw new InvalidOperationException("Peer authentication requires a current user to load an identity certificate for.");
            return ApplyMsmt(fallback.ConfigureConnectionOptions(MsmtCertificateLookup.BuildPeerOptionsFromFiles(config.GetCertificatePath(userName)!, authorityFile)));
        }
    }

    /// <inheritdoc />
    public MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options) => ApplyMsmt(fallback.ConfigureConnectionOptions(options));

    /// <inheritdoc />
    public HdlcPeerOptions HdlcOptions => Current?.Hdlc is { } overrides ? fallback.HdlcOptions.Overlay(overrides) : fallback.HdlcOptions;

    private MsmtSessionPeerOptions ApplyMsmt(MsmtSessionPeerOptions options)
    {
        if (Current?.Msmt is not { } overrides)
        {
            return options;
        }

        MsmtConnectionOptions merged = new MsmtConnectionOptions
        {
            HandshakeTimeout = options.HandshakeTimeout,
            StallTimeout = options.StallTimeout,
            ResponseTimeout = options.ResponseTimeout,
            TcpKeepAliveTime = options.TcpKeepAliveTime,
            MaximumSessionLifetime = options.MaximumSessionLifetime,
            SessionLifetime = options.SessionLifetime,
            KeepAliveMinInterval = options.KeepAliveMinInterval,
            KeepAliveMaxInterval = options.KeepAliveMaxInterval
        }.Overlay(overrides);
        return options with
        {
            HandshakeTimeout = merged.HandshakeTimeout,
            StallTimeout = merged.StallTimeout,
            ResponseTimeout = merged.ResponseTimeout,
            TcpKeepAliveTime = merged.TcpKeepAliveTime,
            MaximumSessionLifetime = merged.MaximumSessionLifetime,
            SessionLifetime = merged.SessionLifetime,
            KeepAliveMinInterval = merged.KeepAliveMinInterval,
            KeepAliveMaxInterval = merged.KeepAliveMaxInterval
        };
    }

    /// <inheritdoc />
    public IHandshakeHandler? PacketHandshakeHandler => fallback.PacketHandshakeHandler;

    /// <inheritdoc />
    public IHandshakeHandler? FrameHandshakeHandler => fallback.FrameHandshakeHandler;

    /// <inheritdoc />
    public bool CommandLineOverridesAllowed => fallback.CommandLineOverridesAllowed;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />

    /// <inheritdoc />
    public IEngineFrameHandler? FrameHandler => fallback.FrameHandler;
    /// <inheritdoc />
    public IReadOnlyList<ExportFormatDefinition> ExportFormats => fallback.ExportFormats;
    /// <inheritdoc />
    public IReadOnlyList<ImportFormatDefinition> ImportFormats => fallback.ImportFormats;
    /// <inheritdoc />
    public IReadOnlyList<string> StorageServers => fallback.StorageServers;
    /// <inheritdoc />
    public IReadOnlyList<AutoForwarderDefinition> AutoForwarders => fallback.AutoForwarders;

    /// <inheritdoc />
    public string? FindUserName(string name) => fallback.FindUserName(name);
    /// <inheritdoc />
    public string? GetCertificateProblem(string userName) => fallback.GetCertificateProblem(userName);

    /// <inheritdoc />
    public UserInfo GetUserInfo(string userName) => fallback.GetUserInfo(userName);
    /// <inheritdoc />
    public IConnectionInfo WithLocalUser(IConnectionInfo connection) => connection is ConnectionInfo info ? info with { LocalUser = currentUserProvider.UserName } : connection;
}
