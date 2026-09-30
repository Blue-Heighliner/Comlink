namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Engine-level decorator applying the network configuration file's node settings for the user this process runs as, over
/// whichever <see cref="IEngineController"/> is built from the host's <see cref="IEngineConfiguration"/> (an
/// <see cref="EngineController"/>): the data folder, the identity certificate file, and the alert, tag and print
/// settings, field by field; every other member, including the entire message-format surface and everything
/// about users (which the wrapped <see cref="EngineController"/> reads from the same file), delegates straight to the wrapped provider.
/// Registered by <see cref="EngineExtensions.UseEngine"/>. The user is the one named on the command line, or else the installed
/// user; only the one named on the command line decides <see cref="AppDataPath"/>, since an installed user is not known
/// until that path has been read.
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

    private NetworkUserConfig? Launched => config.Find(DebugUserName);
    private NetworkUserConfig? Current => config.Find(DebugUserName ?? currentUserProvider.UserName);

    /// <inheritdoc />
    public Type MessageType => fallback.MessageType;
    /// <inheritdoc />
    public INetworkSerializer NetworkSerializer => fallback.NetworkSerializer;
    /// <inheritdoc />
    public Type? PacketType => fallback.PacketType;
    /// <inheritdoc />
    public INetworkSerializer? PacketSerializer => fallback.PacketSerializer;
    /// <inheritdoc />
    public int PacketSize => fallback.PacketSize;
    /// <inheritdoc />
    public int PacketWindow => fallback.PacketWindow;
    /// <inheritdoc />
    public object CreateMessage() => fallback.CreateMessage();
    /// <inheritdoc />
    public string GetMessageId(object message) => fallback.GetMessageId(message);
    /// <inheritdoc />
    public void SetMessageId(object message, string value) => fallback.SetMessageId(message, value);
    /// <inheritdoc />
    public string GetFromUser(object message) => fallback.GetFromUser(message);
    /// <inheritdoc />
    public void SetFromUser(object message, string value) => fallback.SetFromUser(message, value);
    /// <inheritdoc />
    public string GetSubject(object message) => fallback.GetSubject(message);
    /// <inheritdoc />
    public void SetSubject(object message, string value) => fallback.SetSubject(message, value);
    /// <inheritdoc />
    public string GetBody(object message) => fallback.GetBody(message);
    /// <inheritdoc />
    public void SetBody(object message, string value) => fallback.SetBody(message, value);
    /// <inheritdoc />
    public List<MessageAddress> GetAddresses(object message) => fallback.GetAddresses(message);
    /// <inheritdoc />
    public void SetAddresses(object message, List<MessageAddress> value) => fallback.SetAddresses(message, value);
    /// <inheritdoc />
    public DateTime GetSentAt(object message) => fallback.GetSentAt(message);
    /// <inheritdoc />
    public void SetSentAt(object message, DateTime value) => fallback.SetSentAt(message, value);
    /// <inheritdoc />
    public string GetConfirmationMessageId(object message) => fallback.GetConfirmationMessageId(message);
    /// <inheritdoc />
    public void SetConfirmationMessageId(object message, string value) => fallback.SetConfirmationMessageId(message, value);
    /// <inheritdoc />
    public bool IsRetrieval(object message) => fallback.IsRetrieval(message);
    /// <inheritdoc />
    public RetrievalCriteria GetRetrieval(object message) => fallback.GetRetrieval(message);
    /// <inheritdoc />
    public void SetRetrieval(object message, RetrievalCriteria criteria) => fallback.SetRetrieval(message, criteria);
    /// <inheritdoc />
    public bool GetIsAlert(object message) => fallback.GetIsAlert(message);
    /// <inheritdoc />
    public void SetIsAlert(object message, bool value) => fallback.SetIsAlert(message, value);
    /// <inheritdoc />
    public int GetPriority(object message) => fallback.GetPriority(message);
    /// <inheritdoc />
    public void SetPriority(object message, int value) => fallback.SetPriority(message, value);
    /// <inheritdoc />
    public string GetTag(object message) => fallback.GetTag(message);
    /// <inheritdoc />
    public void SetTag(object message, string value) => fallback.SetTag(message, value);
    /// <inheritdoc />
    public string GetSecurityLevel(object message) => fallback.GetSecurityLevel(message);
    /// <inheritdoc />
    public void SetSecurityLevel(object message, string value) => fallback.SetSecurityLevel(message, value);
    /// <inheritdoc />
    public object CreatePacket() => fallback.CreatePacket();
    /// <inheritdoc />
    public int GetPayloadId(object packet) => fallback.GetPayloadId(packet);
    /// <inheritdoc />
    public void SetPayloadId(object packet, int value) => fallback.SetPayloadId(packet, value);
    /// <inheritdoc />
    public int GetPacketIndex(object packet) => fallback.GetPacketIndex(packet);
    /// <inheritdoc />
    public void SetPacketIndex(object packet, int value) => fallback.SetPacketIndex(packet, value);
    /// <inheritdoc />
    public int GetPacketCount(object packet) => fallback.GetPacketCount(packet);
    /// <inheritdoc />
    public void SetPacketCount(object packet, int value) => fallback.SetPacketCount(packet, value);
    /// <inheritdoc />
    public int GetPayloadLength(object packet) => fallback.GetPayloadLength(packet);
    /// <inheritdoc />
    public void SetPayloadLength(object packet, int value) => fallback.SetPayloadLength(packet, value);
    /// <inheritdoc />
    public ReadOnlyMemory<byte> GetPacketData(object packet) => fallback.GetPacketData(packet);
    /// <inheritdoc />
    public void SetPacketData(object packet, ReadOnlyMemory<byte> value) => fallback.SetPacketData(packet, value);

    /// <inheritdoc />
    public string AppName => fallback.AppName;
    /// <inheritdoc />
    public string AppVersion => fallback.AppVersion;
    /// <inheritdoc />
    public string AppDataPath => Launched?.DataFolder switch
    {
        null => fallback.AppDataPath,
        ['@', ..] folder => Path.Combine(fallback.AppDataPath, folder[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        string folder => folder
    };
    /// <inheritdoc />
    public bool IsKioskMode => fallback.IsKioskMode;
    /// <inheritdoc />
    public string HomeText => fallback.HomeText;
    /// <inheritdoc />
    public Uri? WindowIconUri => fallback.WindowIconUri;

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
    public int GetPrintCount(object message) => fallback.GetPrintCount(message);

    /// <inheritdoc />
    public bool CanDelete(FolderType folderType) => fallback.CanDelete(folderType);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Only one of the current user's <c>CertificateFile</c> and the network's <c>TrustedAuthorityCertificateFile</c> is set - they must be set together.</exception>
    public MsmtSessionPeerOptions ConnectionOptions
    {
        get
        {
            string? peerFile = Current is { } current ? config.GetCertificateFilePath(current) : null;
            string? authorityFile = config.GetTrustedAuthorityCertificateFilePath();
            if (peerFile is null && authorityFile is null)
            {
                return fallback.ConnectionOptions;
            }
            if (peerFile is null || authorityFile is null)
            {
                throw new InvalidOperationException("A user's CertificateFile and the network's TrustedAuthorityCertificateFile must both be set together.");
            }
            return fallback.ConfigureConnectionOptions(MsmtCertificateLookup.BuildPeerOptionsFromFiles(peerFile, authorityFile));
        }
    }

    /// <inheritdoc />
    public MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options) => fallback.ConfigureConnectionOptions(options);

    /// <inheritdoc />
    public MicroGatePeerOptions MicroGateOptions => fallback.MicroGateOptions;

    /// <inheritdoc />
    public Type? ConnectionMessageType => fallback.ConnectionMessageType;
    /// <inheritdoc />
    public Type? ConnectionResponseType => fallback.ConnectionResponseType;
    /// <inheritdoc />
    public INetworkSerializer? ConnectionSerializer => fallback.ConnectionSerializer;

    /// <inheritdoc />
    public bool CommandLineOverridesAllowed => fallback.CommandLineOverridesAllowed;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />
    public IExternalSystem? ExternalServer => fallback.ExternalServer;

    /// <inheritdoc />
    public IReadOnlyList<Action<IUserConnectionHookContext>> UserConnectedHooks => fallback.UserConnectedHooks;
    /// <inheritdoc />
    public IReadOnlyList<Action<IUserConnectionHookContext>> UserDisconnectedHooks => fallback.UserDisconnectedHooks;
    /// <inheritdoc />
    public IReadOnlyList<Action<IMessageReceivedHookContext>> MessageReceivedHooks => fallback.MessageReceivedHooks;
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
    public UserIdentity? IdentifyConnection(ConnectionInfo connection) => fallback.IdentifyConnection(connection);
    /// <inheritdoc />
    public object? CreateConnectionMessage(ConnectionInfo connection) => fallback.CreateConnectionMessage(connection);
    /// <inheritdoc />
    public object? CreateConnectionResponse(ConnectionInfo connection) => fallback.CreateConnectionResponse(connection);
}
