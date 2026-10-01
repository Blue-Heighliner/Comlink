namespace BlueHeighliner.Comlink.Control;

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
    public object CreateMessage(MessageCreateContext context) => fallback.CreateMessage(context);

    /// <inheritdoc />
    public object CreateReadReceipt(string messageId) => fallback.CreateReadReceipt(messageId);

    /// <inheritdoc />
    public object CreateReceiveReceipt(string messageId) => fallback.CreateReceiveReceipt(messageId);

    /// <inheritdoc />
    public object CreateRetrieval(RetrievalCriteria criteria) => fallback.CreateRetrieval(criteria);

    /// <inheritdoc />
    public object CreateFrame() => fallback.CreateFrame();
    /// <inheritdoc />
    public string GetFrameId(object frame) => fallback.GetFrameId(frame);
    /// <inheritdoc />
    public void SetFrameId(object frame, string value) => fallback.SetFrameId(frame, value);
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
    public string GetTag(object frame) => fallback.GetTag(frame);
    /// <inheritdoc />
    public string GetSecurityLevel(object frame) => fallback.GetSecurityLevel(frame);
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
    public string HomeText => fallback.HomeText;
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
    public string AlertLabel => Current?.AlertText is { Length: > 0 } text ? text : fallback.AlertLabel;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => Current?.AlarmSoundSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : fallback.AlarmSoundDuration;
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => Current?.QuickConfirmationEnabled ?? fallback.QuickConfirmationEnabled;
    /// <inheritdoc />
    public bool ComposeAlertsEnabled => Current?.ComposeAlertsEnabled ?? fallback.ComposeAlertsEnabled;

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
                return fallback.ConnectionOptions;
            }
            if (store is null || authorityFile is null)
            {
                throw new InvalidOperationException("The network's CertificateStore and AuthorityCertificate must both be set together.");
            }

            string userName = currentUserProvider.UserName ?? DebugUserName?.ToUpperInvariant() ?? throw new InvalidOperationException("Peer authentication requires a current user to load an identity certificate for.");
            return fallback.ConfigureConnectionOptions(MsmtCertificateLookup.BuildPeerOptionsFromFiles(config.GetCertificatePath(userName)!, authorityFile));
        }
    }

    /// <inheritdoc />
    public MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options) => fallback.ConfigureConnectionOptions(options);

    /// <inheritdoc />
    public MicroGatePeerOptions MicroGateOptions => fallback.MicroGateOptions;

    /// <inheritdoc />
    public IInitialProcessor? InitialPacketProcessor => fallback.InitialPacketProcessor;
    /// <inheritdoc />
    public IInitialProcessor? InitialFrameProcessor => fallback.InitialFrameProcessor;

    /// <inheritdoc />
    public bool CommandLineOverridesAllowed => fallback.CommandLineOverridesAllowed;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />
    public IExternalSystem? ExternalServer => fallback.ExternalServer;

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
    public string? IdentifyConnection(IConnectionInfo connection) => fallback.IdentifyConnection(WithLocalUser(connection));
    /// <inheritdoc />
    public IConnectionInfo WithLocalUser(IConnectionInfo connection) => connection is ConnectionInfo info ? info with { LocalUser = currentUserProvider.UserName } : connection;
}
