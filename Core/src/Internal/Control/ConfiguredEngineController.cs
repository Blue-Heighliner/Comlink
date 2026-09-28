namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Engine-level decorator applying every <c>config.json</c> field over whichever <see cref="IEngineController"/>
/// is built from the host's <see cref="IEngineConfiguration"/> (an <see cref="EngineController"/>) — field by field,
/// for just the members that have a corresponding <c>config.json</c> field; every other member, including the
/// entire message-format surface, delegates straight to the wrapped provider. Registered by
/// <see cref="EngineExtensions.UseEngine"/>.
/// <see cref="ConnectionOptions"/> has no <c>config.json</c> field of its own but is reimplemented (rather than
/// delegated) so it consumes this decorator's own, potentially config-overridden, <see cref="GetCertificateName"/>
/// and <see cref="TrustedAuthorityCertificateName"/> instead of the wrapped provider's raw ones.
/// </summary>
internal sealed class ConfiguredEngineController : IEngineController
{
    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional overrides.</param>
    /// <param name="currentUserProvider">Tracks the user name of the currently running instance, needed by <see cref="ConnectionOptions"/>.</param>
    public ConfiguredEngineController(IEngineController fallback, EngineConfigFile config, ICurrentUserProvider currentUserProvider)
    {
        this.fallback = fallback;
        this.config = config;
        this.currentUserProvider = currentUserProvider;
        userData = config.GetUserData();
    }

    private readonly IEngineController fallback;
    private readonly EngineConfigFile config;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> userData;

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
    public string AppDataPath => config.DataFolder switch
    {
        null => fallback.AppDataPath,
        ['@', ..] => Path.Combine(fallback.AppDataPath, config.DataFolder[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
        _ => config.DataFolder
    };
    /// <inheritdoc />
    public bool IsKioskMode => fallback.IsKioskMode;
    /// <inheritdoc />
    public string HomeText => fallback.HomeText;
    /// <inheritdoc />
    public Uri? WindowIconUri => fallback.WindowIconUri;

    /// <inheritdoc />
    public string? DebugUserName => config.UserName ?? fallback.DebugUserName;
    /// <inheritdoc />
    public IReadOnlyList<string> Users
    {
        get
        {
            HashSet<string> names = new(fallback.Users, StringComparer.OrdinalIgnoreCase);
            foreach (string user in config.Users.Keys)
            {
                names.Add(user.ToUpperInvariant());
            }
            foreach (string group in config.UserGroups.Keys)
            {
                names.Add(group.ToUpperInvariant());
            }
            return [.. names.OrderBy(n => n)];
        }
    }
    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups
    {
        get
        {
            Dictionary<string, IReadOnlyList<string>> merged = new(fallback.UserGroups, StringComparer.OrdinalIgnoreCase);
            foreach ((string groupName, List<string> members) in config.UserGroups)
            {
                merged[groupName] = members.AsReadOnly();
            }
            return merged;
        }
    }

    /// <inheritdoc />
    public int PeerPort => config.PeerPort ?? fallback.PeerPort;
    /// <inheritdoc />
    public int InterfacePort => config.InterfacePort ?? fallback.InterfacePort;

    /// <inheritdoc />
    public string AlertLabel => string.IsNullOrEmpty(config.AlertText) ? fallback.AlertLabel : config.AlertText;
    /// <inheritdoc />
    public TimeSpan AlarmSoundDuration => config.AlarmSoundSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : fallback.AlarmSoundDuration;
    /// <inheritdoc />
    public bool QuickConfirmationEnabled => config.QuickConfirmationEnabled ?? fallback.QuickConfirmationEnabled;
    /// <inheritdoc />
    public bool ComposeAlertsEnabled => config.ComposeAlertsEnabled ?? fallback.ComposeAlertsEnabled;

    /// <inheritdoc />
    public IReadOnlyList<MessagePriorityOption> Priorities => fallback.Priorities;
    /// <inheritdoc />
    public bool TagsEnabled => config.MessageTagsEnabled ?? fallback.TagsEnabled;
    /// <inheritdoc />
    public string TagLabel => string.IsNullOrEmpty(config.MessageTagLabel) ? fallback.TagLabel : config.MessageTagLabel;
    /// <inheritdoc />
    public IReadOnlyList<TagPriorityBlock> BlockedCombinations => fallback.BlockedCombinations;
    /// <inheritdoc />
    public IReadOnlyList<AddressTypeOption> AddressTypes => fallback.AddressTypes;

    /// <inheritdoc />
    public bool PrintReceivedDefaultEnabled => config.PrintReceivedEnabled ?? fallback.PrintReceivedDefaultEnabled;
    /// <inheritdoc />
    public int GetPrintCount(object message) => fallback.GetPrintCount(message);

    /// <inheritdoc />
    public bool CanDelete(FolderType folderType) => fallback.CanDelete(folderType);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Only one of <c>PeerCertificateFile</c>/<c>TrustedAuthorityCertificateFile</c> is set - they must be set together.</exception>
    public MsmtSessionPeerOptions ConnectionOptions
    {
        get
        {
            string? peerFile = config.GetPeerCertificateFilePath();
            string? authorityFile = config.GetTrustedAuthorityCertificateFilePath();
            if (peerFile is null && authorityFile is null)
            {
                return MsmtCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName, TrustedAuthorityCertificateName);
            }
            if (peerFile is null || authorityFile is null)
            {
                throw new InvalidOperationException("PeerCertificateFile and TrustedAuthorityCertificateFile must both be set together.");
            }
            return MsmtCertificateLookup.BuildPeerOptionsFromFiles(peerFile, authorityFile);
        }
    }

    /// <inheritdoc />
    public NodeRole Role =>
        config.NodeRole is not null && Enum.TryParse(config.NodeRole, ignoreCase: true, out NodeRole role)
            ? role
            : fallback.Role;

    /// <inheritdoc />
    public IReadOnlyList<ConnectionPoint> OutgoingPoints => config.OutgoingPoints.Count > 0 ? config.GetOutgoingPoints() : fallback.OutgoingPoints;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ServerUserConfig> Servers
    {
        get
        {
            Dictionary<string, ServerUserConfig> merged = new(fallback.Servers, StringComparer.OrdinalIgnoreCase);
            foreach ((string serverName, ServerUserConfig serverConfig) in config.GetServerUsers())
            {
                merged[serverName] = serverConfig;
            }
            return merged;
        }
    }

    /// <inheritdoc />
    public Type? ConnectionMessageType => fallback.ConnectionMessageType;
    /// <inheritdoc />
    public Type? ConnectionResponseType => fallback.ConnectionResponseType;
    /// <inheritdoc />
    public INetworkSerializer? ConnectionSerializer => fallback.ConnectionSerializer;

    /// <inheritdoc />
    public bool ConfigFileEnabled => fallback.ConfigFileEnabled;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />
    public IExternalSystem? ExternalServer => fallback.ExternalServer;

    /// <inheritdoc />
    public UserInfo? ResolveCode(string userCode) => fallback.ResolveCode(userCode);
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetUserData(string userName)
    {
        IReadOnlyDictionary<string, string> inherited = fallback.GetUserData(userName);
        if (!userData.TryGetValue(userName, out IReadOnlyDictionary<string, string>? configured) || configured.Count == 0) { return inherited; }

        Dictionary<string, string> merged = new(inherited);
        foreach ((string key, string value) in configured) { merged[key] = value; }
        return merged;
    }
    /// <inheritdoc />
    public UserIdentity? IdentifyConnection(ConnectionInfo connection) => fallback.IdentifyConnection(connection);
    /// <inheritdoc />
    public object? CreateConnectionMessage(ConnectionInfo connection) => fallback.CreateConnectionMessage(connection);
    /// <inheritdoc />
    public object? CreateConnectionResponse(ConnectionInfo connection) => fallback.CreateConnectionResponse(connection);
    /// <inheritdoc />
    public string GetCertificateName(string userName)
        => config.PeerCertificateName is { } ownName && string.Equals(userName, currentUserProvider.UserName, StringComparison.OrdinalIgnoreCase)
            ? ownName
            : fallback.GetCertificateName(userName);

    /// <inheritdoc />
    public string TrustedAuthorityCertificateName
        => config.TrustedAuthorityCertificateName ?? fallback.TrustedAuthorityCertificateName;
}
