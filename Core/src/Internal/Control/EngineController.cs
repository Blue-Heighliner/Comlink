namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Single control interface consolidating every extension point through which a host application
/// customises Engine behaviour without modifying Engine code: the concrete message type and its logical
/// field mapping, how that message type is serialized and packetized (and at what packet size and window) for the network, app
/// identity/presentation, local user identity, the user/group directory, listener ports, alert settings,
/// message composition, the automatic print policy, MSMT peer certificate naming and peer options, network
/// topology, the points this node connects out to, how the user on the other end of a connection is identified
/// (optionally after a connection message exchange), the external systems this instance communicates with, the
/// hooks run on connection and message activity, and whether <c>config.json</c> is read at all. External drive discovery and printer discovery/driving are real
/// OS-level behavior, not configuration or rules, so they live on <see cref="Devices.IExternalDriveProvider"/>
/// and <see cref="Devices.IPrintDriver"/> instead. See <c>Docs/Components/Configuration.md</c>.
/// </summary>
internal interface IEngineController
{
    /// <summary>
    /// The concrete message type used throughout the engine. Must be a type LiteDB can serialize for
    /// storage, and must satisfy whatever <see cref="NetworkSerializer"/> requires for wire transport - the
    /// default <see cref="ProtobufNetworkSerializer"/> requires it to carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes.
    /// </summary>
    Type MessageType { get; }

    /// <summary>
    /// Serializes and deserializes instances of <see cref="MessageType"/> to and from the bytes actually
    /// sent across the network. Defaults to <see cref="ProtobufNetworkSerializer"/>; override to use a
    /// different wire format, as long as every node this instance talks to is configured the same way.
    /// </summary>
    INetworkSerializer NetworkSerializer { get; }

    /// <summary>
    /// The concrete packet type payloads are broken into for the network, or <see langword="null"/> (the default)
    /// for no packetization, in which case full payloads are sent as they are. When set, every payload is cut into
    /// prioritized packets of this type and reassembled on the other side, so a large payload does not hold up
    /// higher-priority ones queued behind it; the engine does all of that itself, and this type only says how a
    /// packet looks on the wire, through <see cref="PacketSerializer"/> and the packet field members below. Every
    /// node this instance talks to must be configured alike, since neither side can tell whether the other
    /// packetizes. Peer, client and server traffic is packetized; interface connections never are.
    /// </summary>
    Type? PacketType { get; }

    /// <summary>
    /// Serializes and deserializes instances of <see cref="PacketType"/> to and from the bytes actually sent
    /// across the network, like <see cref="NetworkSerializer"/> does for messages. <see langword="null"/> exactly
    /// when <see cref="PacketType"/> is.
    /// </summary>
    INetworkSerializer? PacketSerializer { get; }

    /// <summary>
    /// The largest a serialized packet may be, in bytes. Smaller packets let a higher-priority payload cut in
    /// sooner; larger ones carry less framing overhead. The engine works out how much payload fits in a packet
    /// by measuring what <see cref="PacketSerializer"/> makes of one, so it must leave room for the packet's own
    /// fields. Ignored while <see cref="PacketType"/> is <see langword="null"/>.
    /// </summary>
    int PacketSize { get; }

    /// <summary>
    /// How many packets may be in flight over one connection at once. A higher-priority payload sent meanwhile goes out
    /// as soon as the packets already in flight finish, so the window is how many it can end up waiting behind: 1
    /// (the default) is the most responsive, while a wider window keeps a link with a long round trip busier. Must
    /// be at least 1. Ignored while <see cref="PacketType"/> is <see langword="null"/>.
    /// </summary>
    int PacketWindow { get; }

    /// <summary>The application name, used as the default data folder name and in log headers.</summary>
    string AppName { get; }
    /// <summary>The application version, shown in the title bar and the info popup.</summary>
    string AppVersion { get; }
    /// <summary>Absolute path to the application data directory.</summary>
    string AppDataPath { get; }
    /// <summary><see langword="true"/> to enable kiosk mode, which hides window chrome and restricts navigation.</summary>
    bool IsKioskMode { get; }
    /// <summary>The text displayed in the content area when no entry is selected.</summary>
    string HomeText { get; }
    /// <summary>Optional <c>avares://</c> URI of the window icon to apply to the main window, or <see langword="null"/> to use the OS default.</summary>
    Uri? WindowIconUri { get; }

    /// <summary>The overridden user name for development/testing, or <see langword="null"/> if no override is active.</summary>
    string? DebugUserName { get; }
    /// <summary>Every known user and group name in the messaging system.</summary>
    IReadOnlyList<string> Users { get; }
    /// <summary>Every defined group as a map of group name to member names (which may be user names or other group names).</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups { get; }

    /// <summary>Port for the inbound peer-to-peer listener.</summary>
    int PeerPort { get; }
    /// <summary>Port for the inbound interface listener. Always active, regardless of mode.</summary>
    int InterfacePort { get; }

    /// <summary>Text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label.</summary>
    string AlertLabel { get; }
    /// <summary>
    /// How long the alarm sound plays after an alert is received before it is automatically stopped
    /// (see <see cref="IAlertSoundPlayer.Stop"/>). Resets (restarts from this full duration) whenever
    /// a new alert is received while already alarming. Does not affect the alert box itself, which stays
    /// visible until every pending alert has been read.
    /// </summary>
    TimeSpan AlarmSoundDuration { get; }
    /// <summary>
    /// When <see langword="true"/>, clicking the alert box, or pressing Space/Enter while focus is not in
    /// a text input, confirms (marks read) the latest unconfirmed alert. Repeating the action confirms
    /// pending alerts one at a time, most-recently-received first.
    /// </summary>
    bool QuickConfirmationEnabled { get; }
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows the alert checkbox so the user can mark and send
    /// a draft as an alert. When <see langword="false"/>, the checkbox is hidden and a draft can never be
    /// composed or sent as an alert from this app — alerts can still arrive from and be raised for a
    /// peer-originated message.
    /// </summary>
    bool ComposeAlertsEnabled { get; }

    /// <summary>Every selectable priority level, in display order.</summary>
    IReadOnlyList<MessagePriorityOption> Priorities { get; }
    /// <summary>
    /// When <see langword="true"/>, the draft editor shows a tag input and the entry listing shows each
    /// message's tag next to its priority. When <see langword="false"/>, tags are hidden everywhere in the
    /// UI — the underlying <see cref="GetTag"/>/<see cref="SetTag"/> values on existing messages are left
    /// untouched, just not surfaced.
    /// </summary>
    bool TagsEnabled { get; }
    /// <summary>
    /// The label used for the tag input's watermark in the draft editor. Lets a host call the concept
    /// something other than "Tag" (e.g. "Category", "Type") without changing engine behavior.
    /// </summary>
    string TagLabel { get; }
    /// <summary>Every blocked tag/priority combination rule, enforced when composing a draft.</summary>
    IReadOnlyList<TagPriorityBlock> BlockedCombinations { get; }
    /// <summary>
    /// Every address type, in a fixed order (<see cref="AddressType.To"/>, <see cref="AddressType.Cc"/>,
    /// <see cref="AddressType.External"/>), paired with its display label - shown in the address type picker, the
    /// per-address badge, and the message view's section headers. A label defaults to the enum name unless overridden
    /// with <see cref="IEngineBuilder.AddressTypeLabel"/>.
    /// </summary>
    IReadOnlyList<AddressTypeOption> AddressTypes { get; }
    /// <summary>
    /// Every configured security level, in ascending order (index 0 is lowest); empty when
    /// <see cref="IEngineBuilder.SecurityLevels"/> was never stated, which turns the whole feature off. A
    /// message may only be sent at one of these levels, and a destination user's own assigned level (see
    /// <see cref="GetUserSecurityLevel"/>) must rank at or above it.
    /// </summary>
    IReadOnlyList<SecurityLevel> SecurityLevels { get; }

    /// <summary>
    /// Returns the security level name the given user runs at; see <see cref="IEngineBuilder.UserSecurityLevel(string,string)"/>.
    /// Defaults to the lowest configured level for a user with no assignment, or an empty string when no security
    /// levels are configured at all.
    /// </summary>
    /// <param name="userName">The user name to resolve a security level for.</param>
    string GetUserSecurityLevel(string userName);

    /// <summary>
    /// When <see langword="true"/>, the print manager's "print received" toggle (<see cref="ViewModels.IPrintManagerViewModel.PrintReceivedEnabled"/>)
    /// starts enabled, so every received message is automatically added to the print queue from the moment
    /// the app starts. The user can still toggle it off at any time.
    /// </summary>
    bool PrintReceivedDefaultEnabled { get; }

    /// <summary>The peer options - including TLS identity certificate and trusted certificate authorities - used for both inbound and outbound MSMT session peer connections.</summary>
    MsmtSessionPeerOptions ConnectionOptions { get; }

    /// <summary>The configured role for this instance.</summary>
    NodeRole Role { get; }
    /// <summary>
    /// The points this node connects out to, and keeps connected: IP hosts and ports of other nodes to dial, and
    /// serial ports to open. A node configures only where it connects and listens (see <see cref="PeerPort"/>),
    /// never which users it expects there: who is on the other end of a connection is worked out when it forms, by
    /// <see cref="IdentifyConnection"/>. A serial cable joins two nodes and is opened from both ends, so a serial
    /// port is listed here on each. A <see cref="NodeRole.Client"/> connects to the first point only.
    /// </summary>
    IReadOnlyList<ConnectionPoint> OutgoingPoints { get; }
    /// <summary>
    /// The server topology a <see cref="NodeRole.Server"/> instance routes with, keyed by server user name
    /// (case-insensitive): every server in the cluster, not just the local one, and the child clients each owns. It
    /// says who belongs where, not how to reach them, so a connection is matched to a server or child by the identity
    /// <see cref="IdentifyConnection"/> gives it. Unused outside <see cref="NodeRole.Server"/>.
    /// </summary>
    IReadOnlyDictionary<string, ServerUserConfig> Servers { get; }

    /// <summary>
    /// The type of the connection message, or <see langword="null"/> (the default) for none. When set, the node that
    /// opens a connection sends one (both ends of a serial link do) as the first thing on it, built by
    /// <see cref="CreateConnectionMessage"/>, and the connection is not usable, nor identified, until the exchange
    /// completes. Every node must be configured alike, since neither side can tell whether the other expects one.
    /// </summary>
    Type? ConnectionMessageType { get; }
    /// <summary>
    /// The type of the connection response, or <see langword="null"/> (the default) for none. Only used while
    /// <see cref="ConnectionMessageType"/> is set: the node that receives a connection message answers it with a response
    /// built by <see cref="CreateConnectionResponse"/>, which the opening node waits for before the connection is usable.
    /// </summary>
    Type? ConnectionResponseType { get; }
    /// <summary>
    /// Serializes and deserializes connection messages and responses. <see langword="null"/> exactly when
    /// <see cref="ConnectionMessageType"/> is. Defaults to a <see cref="ProtobufNetworkSerializer"/> that builds only
    /// the message and response types.
    /// </summary>
    INetworkSerializer? ConnectionSerializer { get; }
    /// <summary>When <see langword="true"/>, a <c>--config</c> argument is read; when <see langword="false"/> (the default), it is ignored and <see cref="EngineConfigFile"/> always uses its defaults.</summary>
    bool ConfigFileEnabled { get; }

    /// <summary>
    /// The external systems this instance communicates with — each a conduit relaying messages to and
    /// from a system outside Comlink (a socket, a message queue, an HTTP long-poll, etc.). Resolved once
    /// at startup by <c>ExternalSystemsService</c>: every message this instance receives (from a peer, or
    /// from any other external system) is sent through every other external system in this list, and
    /// every message received from one is processed exactly like an ordinary received message. See
    /// <c>Docs/Components/ExternalSystems.md</c>.
    /// </summary>
    IReadOnlyList<IExternalSystem> ExternalSystems { get; }

    /// <summary>
    /// The single external system, from <see cref="ExternalSystems"/>, that should exclusively receive
    /// every message this instance would otherwise send out — whether composed locally by the user or
    /// received from a peer connection — instead of that message going to the normal peer network and
    /// every other configured external system. A message received <em>from</em> this external system is,
    /// in turn, relayed to every other external system exactly as it would be without an
    /// <see cref="ExternalServer"/> configured — only <see cref="ExternalServer"/> itself is treated as
    /// the exclusive upstream hub, everything else still fans out normally. <see langword="null"/> (the
    /// default) disables this gateway behavior entirely, so every configured external system and the
    /// normal peer network behave exactly as they would with no <see cref="ExternalServer"/> at all. Must
    /// be one of the same instances also returned by <see cref="ExternalSystems"/>, so its own
    /// connect/poll/receive lifecycle still runs — this property only designates which one, if any, acts
    /// as the exclusive upstream hub. See <c>Docs/Components/ExternalSystems.md</c>.
    /// </summary>
    IExternalSystem? ExternalServer { get; }

    /// <summary>Every hook run when a user goes from having no live peer connection to having at least one.</summary>
    IReadOnlyList<Action<IUserConnectionHookContext>> UserConnectedHooks { get; }

    /// <summary>Every hook run when a user goes from having at least one live peer connection to having none.</summary>
    IReadOnlyList<Action<IUserConnectionHookContext>> UserDisconnectedHooks { get; }

    /// <summary>Every hook run whenever this instance receives a new (non-confirmation) message from a peer.</summary>
    IReadOnlyList<Action<IMessageReceivedHookContext>> MessageReceivedHooks { get; }

    /// <summary>Creates a new, empty instance of <see cref="MessageType"/>.</summary>
    object CreateMessage();
    /// <summary>Gets the application-level message identifier from <paramref name="message"/>.</summary>
    string GetMessageId(object message);
    /// <summary>Sets the application-level message identifier on <paramref name="message"/>.</summary>
    void SetMessageId(object message, string value);
    /// <summary>Gets the sender user name from <paramref name="message"/>.</summary>
    string GetFromUser(object message);
    /// <summary>Sets the sender user name on <paramref name="message"/>.</summary>
    void SetFromUser(object message, string value);
    /// <summary>Gets the subject line from <paramref name="message"/>.</summary>
    string GetSubject(object message);
    /// <summary>Sets the subject line on <paramref name="message"/>.</summary>
    void SetSubject(object message, string value);
    /// <summary>Gets the body text from <paramref name="message"/>.</summary>
    string GetBody(object message);
    /// <summary>Sets the body text on <paramref name="message"/>.</summary>
    void SetBody(object message, string value);
    /// <summary>Gets the recipient address list from <paramref name="message"/>.</summary>
    List<MessageAddress> GetAddresses(object message);
    /// <summary>Sets the recipient address list on <paramref name="message"/>.</summary>
    void SetAddresses(object message, List<MessageAddress> value);
    /// <summary>Gets the UTC sent timestamp from <paramref name="message"/>.</summary>
    DateTime GetSentAt(object message);
    /// <summary>Sets the UTC sent timestamp on <paramref name="message"/>.</summary>
    void SetSentAt(object message, DateTime value);
    /// <summary>
    /// Gets the message ID this message is a user-read confirmation for, or an empty string if
    /// <paramref name="message"/> is not a confirmation. A confirmation message carries only this field
    /// (plus <see cref="GetMessageId"/>/<see cref="GetFromUser"/> for its own transport) — subject, body,
    /// and addresses are left unset — and is sent back to the original sender when the recipient opens the
    /// referenced message, so the sender can advance that message's delivery status to <c>Read</c>. See
    /// <c>Docs/Components/Peer.md</c>.
    /// </summary>
    string GetConfirmationMessageId(object message);
    /// <summary>Sets the message ID <paramref name="message"/> is a user-read confirmation for.</summary>
    void SetConfirmationMessageId(object message, string value);
    /// <summary>
    /// Gets whether <paramref name="message"/> is an alert: an ordinary message that also causes the
    /// receiving Client-mode UI to alarm (visually and audibly) until the user reads it. See
    /// <c>Docs/Components/ViewModels.md</c>.
    /// </summary>
    bool GetIsAlert(object message);
    /// <summary>Sets whether <paramref name="message"/> is an alert.</summary>
    void SetIsAlert(object message, bool value);
    /// <summary>
    /// Gets the priority number of <paramref name="message"/>. One of the values returned by
    /// <see cref="Priorities"/>; used verbatim as the MSMT send priority (larger values are sent first —
    /// see <c>Docs/Components/Peer.md</c>) whenever this message is sent over an MSMT connection.
    /// </summary>
    int GetPriority(object message);
    /// <summary>Sets the priority number on <paramref name="message"/>.</summary>
    void SetPriority(object message, int value);
    /// <summary>
    /// Gets the short, user-inputted tag identifying the type of message this is, or an empty string if
    /// none was set. See <see cref="BlockedCombinations"/>.
    /// </summary>
    string GetTag(object message);
    /// <summary>Sets the tag on <paramref name="message"/>.</summary>
    void SetTag(object message, string value);
    /// <summary>
    /// Gets the security level name <paramref name="message"/> was sent at, one of <see cref="SecurityLevels"/>, or
    /// an empty string when no security levels are configured.
    /// </summary>
    string GetSecurityLevel(object message);
    /// <summary>Sets the security level name on <paramref name="message"/>.</summary>
    void SetSecurityLevel(object message, string value);

    /// <summary>Creates a new, empty instance of <see cref="PacketType"/>. Only called while <see cref="PacketType"/> is set.</summary>
    object CreatePacket();
    /// <summary>Gets the identifier shared by every packet of one payload, which tells packets of different payloads apart.</summary>
    int GetPayloadId(object packet);
    /// <summary>Sets the payload identifier on <paramref name="packet"/>.</summary>
    void SetPayloadId(object packet, int value);
    /// <summary>Gets the zero-based position of <paramref name="packet"/> among the packets of its payload.</summary>
    int GetPacketIndex(object packet);
    /// <summary>Sets the position of <paramref name="packet"/> among the packets of its payload.</summary>
    void SetPacketIndex(object packet, int value);
    /// <summary>Gets how many packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    int GetPacketCount(object packet);
    /// <summary>Sets the number of packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    void SetPacketCount(object packet, int value);
    /// <summary>Gets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    int GetPayloadLength(object packet);
    /// <summary>Sets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    void SetPayloadLength(object packet, int value);
    /// <summary>Gets the slice of the payload <paramref name="packet"/> carries.</summary>
    ReadOnlyMemory<byte> GetPacketData(object packet);
    /// <summary>Sets the slice of the payload <paramref name="packet"/> carries; the value is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    void SetPacketData(object packet, ReadOnlyMemory<byte> value);

    /// <summary>Resolves <paramref name="userCode"/> to its <see cref="UserInfo"/>, or <see langword="null"/> if the code is unrecognized.</summary>
    /// <param name="userCode">The user installation code to resolve.</param>
    UserInfo? ResolveCode(string userCode);
    /// <summary>
    /// Returns the app-specific information attached to <paramref name="userName"/>, an empty map when there is none. The
    /// engine does not interpret it: it travels with the user's <see cref="UserIdentity"/>, so a host can attach whatever
    /// it needs to a user (a role, a station, a display name) and read it back wherever the user is identified.
    /// </summary>
    /// <param name="userName">The user to describe.</param>
    IReadOnlyDictionary<string, string> GetUserData(string userName);

    /// <summary>
    /// Decides who is on the other end of a connection that has just formed, from what is known about it: for IP the
    /// remote host, port and certificate names, for serial the port and address, and the connection message and
    /// response when those are configured. Returns <see langword="null"/> (the default) to let the engine decide:
    /// an IP connection is the user whose <see cref="GetCertificateName"/> matches a name in its certificate (or, when
    /// none does, a user named after that certificate name), and a serial connection is a user named after its port.
    /// A host overrides this to identify by anything else, such as a serial port to user table, or a user name carried
    /// in the connection message. The user's <see cref="UserIdentity.Data"/> is normally <see cref="GetUserData"/>.
    /// </summary>
    /// <param name="connection">What is known about the connection.</param>
    UserIdentity? IdentifyConnection(ConnectionInfo connection);

    /// <summary>
    /// Builds the connection message to send on a connection that has just formed, or returns <see langword="null"/> to
    /// send an empty one. Only called while <see cref="ConnectionMessageType"/> is set.
    /// </summary>
    /// <param name="connection">What is known about the connection.</param>
    object? CreateConnectionMessage(ConnectionInfo connection);
    /// <summary>
    /// Builds the response to the connection message in <see cref="ConnectionInfo.ConnectionMessage"/>, or returns
    /// <see langword="null"/> to send an empty one. Only called while <see cref="ConnectionResponseType"/> is set.
    /// </summary>
    /// <param name="connection">What is known about the connection, including the message just received.</param>
    object? CreateConnectionResponse(ConnectionInfo connection);

    /// <summary>
    /// Returns how many times <paramref name="message"/> should be automatically added to the print queue
    /// when it arrives — <c>0</c> to not print it, <c>1</c> to print it once, <c>2</c> to print two copies, and
    /// so on. Only consulted while the print manager's "print received" toggle is enabled.
    /// </summary>
    /// <param name="message">The received message, in this instance's own <see cref="MessageType"/>.</param>
    int GetPrintCount(object message);

    /// <summary>
    /// Returns whether the user can delete entries in the given root folder type. Consulted by
    /// <see cref="ViewModels.IEntryBarViewModel.DeleteEntry"/> before deleting; when <see langword="false"/>,
    /// the delete is silently skipped.
    /// </summary>
    /// <param name="folderType">The root folder type the entry being deleted belongs to.</param>
    bool CanDelete(FolderType folderType);

    /// <summary>
    /// Returns the certificate subject name (common name) that belongs to the given user. For the current user it
    /// is the identity certificate searched for in the system store; MSMT peer authentication is mandatory - there
    /// is no unauthenticated mode - so when no matching certificate exists, startup throws. For any other user it is
    /// the name that user's certificate is expected to carry, which a Server uses to recognize who connected to it.
    /// </summary>
    /// <param name="userName">The user name to resolve a certificate name for.</param>
    string GetCertificateName(string userName);

    /// <summary>
    /// The certificate subject name, searched for in the system store the same way as <see
    /// cref="GetCertificateName"/>, of the certificate authority trusted to sign every peer's identity
    /// certificate (see <see cref="GetCertificateName"/>). When no matching certificate exists, startup
    /// throws.
    /// </summary>
    string TrustedAuthorityCertificateName { get; }
}

/// <summary>
/// Implements <see cref="IEngineController"/> from what a host stated on an <see cref="EngineBuilder"/>, with a
/// default for every setting the host left alone. The message and packet members work through the maps the host's
/// mappings were turned into. <c>config.json</c> is applied on top by <see cref="ConfiguredEngineController"/>. Not
/// sealed and every member is virtual so tests can replace a single behavior.
/// </summary>
/// <param name="builder">What the host stated.</param>
/// <param name="currentUserProvider">Tracks the user name of the currently running instance, read for <see cref="ConnectionOptions"/>.</param>
internal class EngineController(EngineBuilder builder, ICurrentUserProvider currentUserProvider) : IEngineController
{
    private readonly MessageMap message = builder.MessageMap ?? throw new InvalidOperationException("The engine configuration must state its message type with Message<TMessage>(...).");
    private readonly PacketMap? packet = builder.PacketMap;
    private readonly IReadOnlyList<MessagePriorityOption> defaultPriorities = [new MessagePriorityOption { Name = "Normal", Value = 0 }];
    private readonly IReadOnlyList<AddressType> addressTypeOrder = [AddressType.To, AddressType.Cc, AddressType.External];
    private readonly Dictionary<string, string> noData = [];
    private INetworkSerializer? connectionSerializer;

    /// <inheritdoc />
    public virtual Type MessageType => message.Type;
    /// <inheritdoc />
    public virtual INetworkSerializer NetworkSerializer => message.Serializer;
    /// <inheritdoc />
    public virtual Type? PacketType => packet?.Type;
    /// <inheritdoc />
    public virtual INetworkSerializer? PacketSerializer => packet?.Serializer;
    /// <inheritdoc />
    public virtual int PacketSize => packet?.Size ?? 16 * 1024;
    /// <inheritdoc />
    public virtual int PacketWindow => packet?.Window ?? 1;

    /// <inheritdoc />
    public virtual string AppName => builder.AppNameValue ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <inheritdoc />
    public virtual string AppVersion => builder.AppVersionValue ?? (Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0");
    /// <inheritdoc />
    public virtual string AppDataPath => builder.DataPathValue ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    /// <inheritdoc />
    public virtual bool IsKioskMode => builder.IsKioskMode;
    /// <inheritdoc />
    public virtual string HomeText => builder.HomeTextValue ?? "HOME";
    /// <inheritdoc />
    public virtual Uri? WindowIconUri => builder.WindowIconValue;

    /// <inheritdoc />
    public virtual string? DebugUserName => builder.DebugUserValue;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> Users => builder.UserNames;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups => builder.UserGroups;

    /// <inheritdoc />
    public virtual int PeerPort => builder.PeerPortValue ?? 50021;
    /// <inheritdoc />
    public virtual int InterfacePort => builder.InterfacePortValue ?? 50020;

    /// <inheritdoc />
    public virtual string AlertLabel => builder.AlertLabelValue ?? "ALERT";
    /// <inheritdoc />
    public virtual TimeSpan AlarmSoundDuration => builder.AlarmDurationValue ?? TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual bool QuickConfirmationEnabled => builder.QuickConfirmationValue ?? true;
    /// <inheritdoc />
    public virtual bool ComposeAlertsEnabled => builder.ComposeAlertsValue ?? true;

    /// <inheritdoc />
    public virtual IReadOnlyList<MessagePriorityOption> Priorities => builder.PriorityOptions.Count > 0 ? builder.PriorityOptions : defaultPriorities;
    /// <inheritdoc />
    public virtual bool TagsEnabled => builder.TagsEnabledValue ?? true;
    /// <inheritdoc />
    public virtual string TagLabel => builder.TagLabelValue ?? "Tag";
    /// <inheritdoc />
    public virtual IReadOnlyList<TagPriorityBlock> BlockedCombinations => builder.BlockedCombinations;
    /// <inheritdoc />
    public virtual IReadOnlyList<AddressTypeOption> AddressTypes
        => [.. addressTypeOrder.Select(type => new AddressTypeOption { Type = type, Label = builder.AddressTypeLabels.TryGetValue(type, out string? label) ? label : type.ToString() })];
    /// <inheritdoc />
    public virtual IReadOnlyList<SecurityLevel> SecurityLevels => builder.SecurityLevelValues;

    /// <inheritdoc />
    public virtual string GetUserSecurityLevel(string userName)
    {
        if (builder.UserSecurityLevelsByName.TryGetValue(userName, out string? stated)) { return stated; }
        string? looked = builder.UserSecurityLevelLookup?.Invoke(userName);
        if (looked is not null) { return looked; }
        return builder.SecurityLevelValues.Count > 0 ? builder.SecurityLevelValues[0].Name : string.Empty;
    }

    /// <inheritdoc />
    public virtual bool PrintReceivedDefaultEnabled => builder.PrintReceivedValue ?? false;

    /// <inheritdoc />
    public virtual MsmtSessionPeerOptions ConnectionOptions => builder.ConnectionOptionsValue?.Invoke()
        ?? MsmtCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName, TrustedAuthorityCertificateName);

    /// <inheritdoc />
    public virtual NodeRole Role => builder.RoleValue ?? NodeRole.Peer;
    /// <inheritdoc />
    public virtual IReadOnlyList<ConnectionPoint> OutgoingPoints => builder.OutgoingPoints;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, ServerUserConfig> Servers => builder.ServerTopology;

    /// <inheritdoc />
    public virtual Type? ConnectionMessageType => builder.ConnectionMessageType;
    /// <inheritdoc />
    public virtual Type? ConnectionResponseType => builder.ConnectionResponseType;
    /// <inheritdoc />
    public virtual INetworkSerializer? ConnectionSerializer => ConnectionMessageType is null
        ? null
        : builder.ConnectionSerializerValue ?? (connectionSerializer ??= new ProtobufNetworkSerializer([.. new[] { ConnectionMessageType, ConnectionResponseType }.OfType<Type>()]));

    /// <inheritdoc />
    public virtual bool ConfigFileEnabled => builder.IsConfigFileEnabled;

    /// <inheritdoc />
    public virtual IReadOnlyList<IExternalSystem> ExternalSystems => builder.ExternalSystems;
    /// <inheritdoc />
    public virtual IExternalSystem? ExternalServer => builder.ExternalServerValue;

    /// <inheritdoc />
    public virtual IReadOnlyList<Action<IUserConnectionHookContext>> UserConnectedHooks => builder.UserConnectedHooks;
    /// <inheritdoc />
    public virtual IReadOnlyList<Action<IUserConnectionHookContext>> UserDisconnectedHooks => builder.UserDisconnectedHooks;
    /// <inheritdoc />
    public virtual IReadOnlyList<Action<IMessageReceivedHookContext>> MessageReceivedHooks => builder.MessageReceivedHooks;

    /// <inheritdoc />
    public virtual string TrustedAuthorityCertificateName => builder.TrustedAuthorityValue ?? "COMLINK-ROOT";

    /// <inheritdoc />
    public virtual object CreateMessage() => message.Create();
    /// <inheritdoc />
    public virtual string GetMessageId(object value) => message.GetId(value);
    /// <inheritdoc />
    public virtual void SetMessageId(object value, string id) => message.SetId(value, id);
    /// <inheritdoc />
    public virtual string GetFromUser(object value) => message.GetSender(value);
    /// <inheritdoc />
    public virtual void SetFromUser(object value, string user) => message.SetSender(value, user);
    /// <inheritdoc />
    public virtual string GetSubject(object value) => message.GetSubject(value);
    /// <inheritdoc />
    public virtual void SetSubject(object value, string subject) => message.SetSubject(value, subject);
    /// <inheritdoc />
    public virtual string GetBody(object value) => message.GetBody(value);
    /// <inheritdoc />
    public virtual void SetBody(object value, string body) => message.SetBody(value, body);
    /// <inheritdoc />
    public virtual List<MessageAddress> GetAddresses(object value) => message.GetAddresses(value);
    /// <inheritdoc />
    public virtual void SetAddresses(object value, List<MessageAddress> addresses) => message.SetAddresses(value, addresses);
    /// <inheritdoc />
    public virtual DateTime GetSentAt(object value) => message.GetSentAt(value);
    /// <inheritdoc />
    public virtual void SetSentAt(object value, DateTime sentAt) => message.SetSentAt(value, sentAt);
    /// <inheritdoc />
    public virtual string GetConfirmationMessageId(object value) => message.GetConfirmationId(value);
    /// <inheritdoc />
    public virtual void SetConfirmationMessageId(object value, string id) => message.SetConfirmationId(value, id);
    /// <inheritdoc />
    public virtual bool GetIsAlert(object value) => message.GetIsAlert(value);
    /// <inheritdoc />
    public virtual void SetIsAlert(object value, bool isAlert) => message.SetIsAlert(value, isAlert);
    /// <inheritdoc />
    public virtual int GetPriority(object value) => message.GetPriority(value);
    /// <inheritdoc />
    public virtual void SetPriority(object value, int priority) => message.SetPriority(value, priority);
    /// <inheritdoc />
    public virtual string GetTag(object value) => message.GetTag(value);
    /// <inheritdoc />
    public virtual void SetTag(object value, string tag) => message.SetTag(value, tag);
    /// <inheritdoc />
    public virtual string GetSecurityLevel(object value) => message.GetSecurityLevel(value);
    /// <inheritdoc />
    public virtual void SetSecurityLevel(object value, string level) => message.SetSecurityLevel(value, level);

    /// <inheritdoc />
    public virtual object CreatePacket() => Packet.Create();
    /// <inheritdoc />
    public virtual int GetPayloadId(object value) => Packet.GetPayloadId(value);
    /// <inheritdoc />
    public virtual void SetPayloadId(object value, int id) => Packet.SetPayloadId(value, id);
    /// <inheritdoc />
    public virtual int GetPacketIndex(object value) => Packet.GetIndex(value);
    /// <inheritdoc />
    public virtual void SetPacketIndex(object value, int index) => Packet.SetIndex(value, index);
    /// <inheritdoc />
    public virtual int GetPacketCount(object value) => Packet.GetCount(value);
    /// <inheritdoc />
    public virtual void SetPacketCount(object value, int count) => Packet.SetCount(value, count);
    /// <inheritdoc />
    public virtual int GetPayloadLength(object value) => Packet.GetPayloadLength(value);
    /// <inheritdoc />
    public virtual void SetPayloadLength(object value, int length) => Packet.SetPayloadLength(value, length);
    /// <inheritdoc />
    public virtual ReadOnlyMemory<byte> GetPacketData(object value) => Packet.GetData(value);
    /// <inheritdoc />
    public virtual void SetPacketData(object value, ReadOnlyMemory<byte> data) => Packet.SetData(value, data);

    /// <inheritdoc />
    public virtual UserInfo? ResolveCode(string userCode)
        => builder.UserCodeResolver is { } resolve
            ? resolve(userCode)
            : userCode.Equals("CODE", StringComparison.OrdinalIgnoreCase)
                ? new UserInfo { Name = "TEST", Code = "CODE" }
                : null;

    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, string> GetUserData(string userName)
    {
        IReadOnlyDictionary<string, string>? looked = builder.UserDataLookup?.Invoke(userName);
        if (!builder.UserDataByName.TryGetValue(userName, out Dictionary<string, string>? stated)) { return looked ?? noData; }

        Dictionary<string, string> merged = looked is null ? [] : new Dictionary<string, string>(looked);
        foreach ((string key, string value) in stated) { merged[key] = value; }
        return merged;
    }

    /// <inheritdoc />
    public virtual UserIdentity? IdentifyConnection(ConnectionInfo connection) => builder.IdentifyValue?.Invoke(connection);
    /// <inheritdoc />
    public virtual object? CreateConnectionMessage(ConnectionInfo connection) => builder.ConnectionMessageFactory?.Invoke(connection);
    /// <inheritdoc />
    public virtual object? CreateConnectionResponse(ConnectionInfo connection) => builder.ConnectionResponseFactory?.Invoke(connection);

    /// <inheritdoc />
    public virtual int GetPrintCount(object value) => builder.PrintCountValue?.Invoke(value) ?? 1;

    /// <inheritdoc />
    public virtual bool CanDelete(FolderType folderType) => builder.CanDeleteValue?.Invoke(folderType) ?? true;

    /// <inheritdoc />
    public virtual string GetCertificateName(string userName) => builder.CertificateNameValue?.Invoke(userName) ?? userName;

    private PacketMap Packet => packet ?? throw new NotSupportedException("This engine has no packet type; state one with Packets<TPacket>(...) to enable packetization.");
}

/// <summary>Extension members for <see cref="IEngineController"/>.</summary>
internal static class EngineControllerExtensions
{
    extension(IEngineController engineController)
    {
        /// <summary>
        /// Reads every logical field of <paramref name="payload"/> (an instance of <see cref="IEngineController.MessageType"/>)
        /// into a new <see cref="MessageReceivedEvent"/>. Shared by <see cref="DirectServiceConnection"/> and
        /// <see cref="EngineHooksService"/>, so both surface the exact same fields for an inbound message.
        /// </summary>
        public MessageReceivedEvent ToMessageReceivedEvent(object payload) => new()
        {
            MessageId = engineController.GetMessageId(payload),
            FromUser = engineController.GetFromUser(payload),
            Subject = engineController.GetSubject(payload),
            Body = engineController.GetBody(payload),
            Addresses = [.. engineController.GetAddresses(payload).Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type.ToString(), Information = a.Information })],
            SentAt = engineController.GetSentAt(payload),
            IsAlert = engineController.GetIsAlert(payload),
            Priority = engineController.GetPriority(payload),
            Tag = engineController.GetTag(payload),
            SecurityLevel = engineController.GetSecurityLevel(payload)
        };
    }
}
