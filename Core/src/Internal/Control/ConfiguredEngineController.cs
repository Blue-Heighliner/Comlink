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
    public int PacketSize => fallback.PacketSize;
    /// <inheritdoc />
    public int PacketWindow => fallback.PacketWindow;
    /// <inheritdoc />
    public object CreateMessage(MessageContent context) => fallback.CreateMessage(context);

    /// <inheritdoc />
    public object CreateReadReceipt(string messageId, string to) => fallback.CreateReadReceipt(messageId, to);

    /// <inheritdoc />
    public object CreateReceiveReceipt(string messageId, string to) => fallback.CreateReceiveReceipt(messageId, to);

    /// <inheritdoc />
    public object CreateRetrieval(RetrievalCriteria criteria, string server) => fallback.CreateRetrieval(criteria, server);
    /// <inheritdoc />
    public IReadOnlyList<string> Route(object frame) => fallback.Route(frame);

    /// <inheritdoc />
    public object CreateFrame() => fallback.CreateFrame();
    /// <inheritdoc />
    public string GetMessageId(object message) => fallback.GetMessageId(message);
    /// <inheritdoc />
    public void SetMessageId(object message, string id) => fallback.SetMessageId(message, id);
    /// <inheritdoc />
    public string GetFromUser(object frame) => fallback.GetFromUser(frame);
    /// <inheritdoc />
    public void SetFromUser(object frame, string value) => fallback.SetFromUser(frame, value);
    /// <inheritdoc />
    public string GetBody(object frame) => fallback.GetBody(frame);
    /// <inheritdoc />
    public List<MessageAddress> GetAddresses(object frame) => fallback.GetAddresses(frame);
    /// <inheritdoc />
    public void SetAddresses(object frame, List<MessageAddress> value) => fallback.SetAddresses(frame, value);
    /// <inheritdoc />
    public DateTime GetSentAt(object frame) => fallback.GetSentAt(frame);
    /// <inheritdoc />
    public string GetReadReceiptMessageId(object frame) => fallback.GetReadReceiptMessageId(frame);
    /// <inheritdoc />
    public bool IsReadReceipt(object frame) => fallback.IsReadReceipt(frame);
    /// <inheritdoc />
    public string GetReceiveReceiptMessageId(object frame) => fallback.GetReceiveReceiptMessageId(frame);

    /// <inheritdoc />
    public bool IsReceiveReceipt(object frame) => fallback.IsReceiveReceipt(frame);
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
    public bool IsRetrieval(object frame) => fallback.IsRetrieval(frame);
    /// <inheritdoc />
    public RetrievalCriteria GetRetrieval(object frame) => fallback.GetRetrieval(frame);
    /// <inheritdoc />
    public bool IsMessage(object frame) => fallback.IsMessage(frame);
    /// <inheritdoc />
    public bool GetIsAlert(object frame) => fallback.GetIsAlert(frame);
    /// <inheritdoc />
    public int GetPriority(object frame) => fallback.GetPriority(frame);
    /// <inheritdoc />
    public string NextId(string? previous) => fallback.NextId(previous);
    /// <inheritdoc />
    public int LowestPriority => fallback.LowestPriority;
    /// <inheritdoc />
    public int HighestPriority => fallback.HighestPriority;
    /// <inheritdoc />
    public Enum ResolvePriority(Enum? priority) => fallback.ResolvePriority(priority);
    /// <inheritdoc />
    public Enum RequirePriority(Enum? priority) => fallback.RequirePriority(priority);
    /// <inheritdoc />
    public bool ComputeIsAlert(string body, Enum? priority, string tag, string securityLevel, IReadOnlyList<AddressRequest> addresses) => fallback.ComputeIsAlert(body, priority, tag, securityLevel, addresses);
    /// <inheritdoc />
    public string? GetUnconfiguredLevelReason(object message) => fallback.GetUnconfiguredLevelReason(message);
    /// <inheritdoc />
    public void Validate() => fallback.Validate();
    /// <inheritdoc />
    public Enum PriorityOf(int? value) => fallback.PriorityOf(value);
    /// <inheritdoc />
    public int StoredPriority(Enum priority) => fallback.StoredPriority(priority);
    /// <inheritdoc />
    public string NameOf(Enum priority) => fallback.NameOf(priority);
    /// <inheritdoc />
    public Enum GetMessagePriority(object message) => fallback.GetMessagePriority(message);
    /// <inheritdoc />
    public string GetTag(object frame) => fallback.GetTag(frame);
    /// <inheritdoc />
    public string GetSecurityLevel(object frame) => fallback.GetSecurityLevel(frame);
    /// <inheritdoc />
    public Enum? GetSecurityLevelKey(object frame) => fallback.GetSecurityLevelKey(frame);
    /// <inheritdoc />
    public string GetSecurityLevelName(Enum? level) => fallback.GetSecurityLevelName(level);
    /// <inheritdoc />
    public object CreateFramePacket(FramePacketCreateContext context) => fallback.CreateFramePacket(context);
    /// <inheritdoc />
    public bool IsFramePacket(object packet) => fallback.IsFramePacket(packet);
    /// <inheritdoc />
    public int GetPayloadId(object packet) => fallback.GetPayloadId(packet);
    /// <inheritdoc />
    public int GetPacketIndex(object packet) => fallback.GetPacketIndex(packet);
    /// <inheritdoc />
    public int GetPacketCount(object packet) => fallback.GetPacketCount(packet);
    /// <inheritdoc />
    public int GetPayloadLength(object packet) => fallback.GetPayloadLength(packet);
    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPacketData(object packet) => fallback.GetPacketData(packet);

    /// <inheritdoc />
    public string AppName => fallback.AppName;
    /// <inheritdoc />
    public string AppVersion => fallback.AppVersion;
    /// <inheritdoc />
    public string AppDataRoot => fallback.AppDataRoot;
    /// <inheritdoc />
    public string AppDataPath => currentUserProvider.UserName is null && DebugUserName is { } debug ? Path.Combine(fallback.AppDataRoot, fallback.AppName, debug.ToUpperInvariant()) : fallback.AppDataPath;
    /// <inheritdoc />
    public string StatePath => fallback.StatePath;
    /// <inheritdoc />
    public bool IsKioskMode => fallback.IsKioskMode;
    /// <inheritdoc />
    public string GetNetworkIndicatorLabel(bool isOnline) => fallback.GetNetworkIndicatorLabel(isOnline);
    /// <inheritdoc />
    public string GetNetworkIndicatorColor(bool isOnline) => fallback.GetNetworkIndicatorColor(isOnline);
    /// <inheritdoc />
    public bool SeparateAlerts => fallback.SeparateAlerts;
    /// <inheritdoc />
    public string HomeText => fallback.HomeText;
    /// <inheritdoc />
    public string PriorityLabel => fallback.PriorityLabel;
    /// <inheritdoc />
    public string PriorityPluralLabel => fallback.PriorityPluralLabel;
    /// <inheritdoc />
    public string SecurityLevelPluralLabel => fallback.SecurityLevelPluralLabel;
    /// <inheritdoc />
    public string SecurityLevelLabel => fallback.SecurityLevelLabel;
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
    public string GetCertificateName(string userName) => fallback.GetCertificateName(userName);
    /// <inheritdoc />
    public string TrustedAuthorityCertificateName => fallback.TrustedAuthorityCertificateName;

    /// <inheritdoc />
    public int PeerPort => fallback.PeerPort;
    /// <inheritdoc />
    public int InterfacePort => fallback.InterfacePort;

    /// <inheritdoc />
    public string AlertPluralLabel => Current?.AlertText is { Length: > 0 } text ? (text.EndsWith('s') ? text : text + "s") : fallback.AlertPluralLabel;
    /// <inheritdoc />
    public string TagPluralLabel => Current?.MessageTagLabel is { Length: > 0 } label ? (label.EndsWith('s') ? label : label + "s") : fallback.TagPluralLabel;
    /// <inheritdoc />
    public string AlertLabel => Current?.AlertText is { Length: > 0 } text ? text : fallback.AlertLabel;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => Current?.AlarmSoundSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : fallback.AlarmSoundDuration;
    /// <inheritdoc />
    public TimeSpan DisconnectAlarmDuration => fallback.DisconnectAlarmDuration;
    /// <inheritdoc />
    public IReadOnlyList<string> AlertQuickReadKeys => fallback.AlertQuickReadKeys;
    /// <inheritdoc />
    public bool AcceptAlert(object message) => fallback.AcceptAlert(message);
    /// <inheritdoc />
    public LineWidthRange? DraftLineWidth => fallback.DraftLineWidth;
    /// <inheritdoc />
    public TagRules DraftTagRules => fallback.DraftTagRules;
    /// <inheritdoc />
    public DraftDefaults DraftDefaults => fallback.DraftDefaults;
    /// <inheritdoc />
    public string? GetDraftHeader(DraftContent draft) => fallback.GetDraftHeader(draft);

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> Priorities => fallback.Priorities;
    /// <inheritdoc />
    public bool TagsEnabled => Current?.MessageTagsEnabled ?? fallback.TagsEnabled;
    /// <inheritdoc />
    public string TagLabel => Current?.MessageTagLabel is { Length: > 0 } label ? label : fallback.TagLabel;
    /// <inheritdoc />
    public IReadOnlyList<TagPriorityBlock> BlockedCombinations => fallback.BlockedCombinations;
    /// <inheritdoc />
    public IReadOnlyList<AddressTypeOption> AddressTypes => fallback.AddressTypes;
    /// <inheritdoc />
    public IReadOnlyList<SecurityLevel> SecurityLevels => fallback.SecurityLevels;
    /// <inheritdoc />
    public string GetUserSecurityLevel(string userName) => fallback.GetUserSecurityLevel(userName);

    /// <inheritdoc />
    public bool PrintReceivedDefaultEnabled => Current?.PrintReceivedEnabled ?? fallback.PrintReceivedDefaultEnabled;
    /// <inheritdoc />
    public int GetPrintCount(object frame) => fallback.GetPrintCount(frame);

    /// <inheritdoc />
    public bool CanDelete(FolderType folderType) => fallback.CanDelete(folderType);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Only one of the network's <c>CertificateStore</c> and <c>AuthorityCertificate</c> is set - they must be set together - or there is no current user to load a certificate for.</exception>
    public MsmtSessionPeerOptions ConnectionOptions
    {
        get
        {
            string? store = config.CertificateStore;
            string? authorityFile = config.GetAuthorityCertificatePath();
            if (store is null && authorityFile is null)
            {
                return ApplyMsmt(fallback.ConnectionOptions);
            }
            if (store is null || authorityFile is null)
            {
                throw new InvalidOperationException("The network's CertificateStore and AuthorityCertificate must both be set together.");
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
        if (Current?.Msmt is not { } overrides) { return options; }

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
    public IInitialProcessor? InitialPacketProcessor => fallback.InitialPacketProcessor;
    /// <inheritdoc />
    public IInitialProcessor? InitialFrameProcessor => fallback.InitialFrameProcessor;

    /// <inheritdoc />
    public bool CommandLineOverridesAllowed => fallback.CommandLineOverridesAllowed;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />

    /// <inheritdoc />
    public INetworkHandler? NetworkHandler => fallback.NetworkHandler;
    /// <inheritdoc />
    public IReadOnlyList<ExportFormatDefinition> ExportFormats => fallback.ExportFormats;
    /// <inheritdoc />
    public IReadOnlyList<ImportFormatDefinition> ImportFormats => fallback.ImportFormats;
    /// <inheritdoc />
    public IReadOnlyList<string> StorageServers => fallback.StorageServers;
    /// <inheritdoc />
    public IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers => fallback.AutoForwardControllers;

    /// <inheritdoc />
    public string? ResolveUserName(string userCode) => fallback.ResolveUserName(userCode);

    /// <inheritdoc />
    public UserInfo GetUserInfo(string userName) => fallback.GetUserInfo(userName);
    /// <inheritdoc />
    public IConnectionInfo WithLocalUser(IConnectionInfo connection) => connection is ConnectionInfo info ? info with { LocalUser = currentUserProvider.UserName } : connection;
}
