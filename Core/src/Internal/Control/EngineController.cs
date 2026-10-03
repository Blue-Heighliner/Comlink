namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Single control interface consolidating every extension point through which a host application
/// customises Engine behaviour without modifying Engine code: the concrete frame type and its logical
/// field mapping, how that frame type is serialized and packetized (and at what packet size and window) for the network, app
/// identity/presentation, local user identity, the user/group directory, listener ports, alert settings,
/// message composition, the automatic print policy, MSMT peer certificate naming and peer options, network
/// topology, the points this node connects out to, how the user on the other end of a connection is identified
/// (optionally after an initial packet and message exchange), the external systems this instance communicates with, the
/// hooks run on connection and message activity, and whether command-line arguments may override the network configuration file and user. External drive discovery and printer discovery/driving are real
/// OS-level behavior, not configuration or rules, so they live on <see cref="Devices.IExternalDriveProvider"/>
/// and <see cref="Devices.IPrintDriver"/> instead. See <c>Docs/Components/Configuration.md</c>.
/// </summary>
internal interface IEngineController
{
    /// <summary>
    /// The concrete frame type used throughout the engine. Must be a type LiteDB can serialize for
    /// storage, and must satisfy whatever <see cref="FrameSerializer"/> requires for wire transport - the
    /// default <see cref="ProtobufSerializer"/> requires it to carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes.
    /// </summary>
    Type FrameType { get; }

    /// <summary>
    /// Serializes and deserializes instances of <see cref="FrameType"/> to and from the bytes actually
    /// sent across the network. Defaults to <see cref="ProtobufSerializer"/>; override to use a
    /// different wire format, as long as every node this instance talks to is configured the same way.
    /// </summary>
    IFrameSerializer FrameSerializer { get; }

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
    /// across the network, like <see cref="FrameSerializer"/> does for messages. <see langword="null"/> exactly
    /// when <see cref="PacketType"/> is.
    /// </summary>
    IPacketSerializer? PacketSerializer { get; }

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

    /// <summary>The application name, used in log headers and as the name of the folder holding the install state.</summary>
    string AppName { get; }
    /// <summary>The application version, shown in the title bar and the info popup.</summary>
    string AppVersion { get; }
    /// <summary>Absolute path to the directory every application data directory lives in: <c>%APPDATA%</c>.</summary>
    string AppDataRoot { get; }
    /// <summary>
    /// Absolute path to the current user's data directory, <c>{AppDataRoot}/{AppName}/{USERNAME}</c>, which holds their database and logs. Before a
    /// user is installed or named there is no user folder yet, so it is <c>{AppDataRoot}/{AppName}</c>.
    /// </summary>
    string AppDataPath { get; }
    /// <summary>
    /// Absolute path to the file remembering which user is installed, <c>{AppDataRoot}/{AppName}/State.json</c>. It lives beside the user
    /// folders rather than in one, since it is what says whose folder to use.
    /// </summary>
    string StatePath { get; }
    /// <summary><see langword="true"/> to enable kiosk mode, which hides window chrome and restricts navigation.</summary>
    bool IsKioskMode { get; }
    /// <summary>The text displayed in the content area when no entry is selected.</summary>
    string HomeText { get; }
    /// <summary>Optional <c>avares://</c> URI or file path of the window icon to apply to the main window, or <see langword="null"/> to use the OS default.</summary>
    string? WindowIconPath { get; }

    /// <summary>The overridden user name for development/testing, or <see langword="null"/> if no override is active.</summary>
    string? DebugUserName { get; }
    /// <summary>Every known user and group name in the messaging system.</summary>
    IReadOnlyList<string> Users { get; }
    /// <summary>Every defined group as a map of group name to member names (which may be user names or other group names).</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups { get; }

    /// <summary>Port for the inbound IP listener: the port of the current user's <see cref="UserInfo.PeerPoint"/>, 50021 by default.</summary>
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
    /// UI — the underlying <see cref="GetTag"/> values on existing messages are left
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
    /// Returns the security level name the given user runs at; see <see cref="UserInfo.SecurityLevel"/>.
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

    /// <summary>Applies the host's adjustment of the MSMT options (see <see cref="IEngineBuilder.MsmtOptions"/>) to <paramref name="options"/>, returning them unchanged if none was stated.</summary>
    MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options);

    /// <summary>The options used for every MicroGate serial connection, after the host's adjustment (see <see cref="IEngineBuilder.MicroGateOptions"/>).</summary>
    MicroGatePeerOptions MicroGateOptions { get; }

    /// <summary>The configured role for this instance.</summary>
    UserRole Role { get; }
    /// <summary>
    /// The points this node connects out to, and keeps connected, worked out from its links: for each link to its parent or a child whose mode is
    /// <see cref="ConnectionMode.MsmtConnect"/>, the other user's <see cref="UserInfo.PeerPoint"/>, and for each <see cref="ConnectionMode.SyncSerial"/> link its serial port
    /// (a serial cable joins two nodes and is opened from both ends, so the port is listed on each); the parent's comes first. A <see cref="UserRole.Peer"/> also dials every other peer that states a
    /// <see cref="UserInfo.PeerPoint"/>, unless both have one and the other's name sorts first, so a pair never connects both ways. Who is on the other end of a connection is still worked out
    /// when it forms, by <see cref="IdentifyConnection"/>.
    /// </summary>
    IReadOnlyList<ConnectionPoint> OutgoingPoints { get; }
    /// <summary>The name of the current user's parent, or <see langword="null"/> for none.</summary>
    string? ParentUser { get; }
    /// <summary>The point this node dials to reach its parent, or <see langword="null"/> when it has no parent or the parent's link is <see cref="ConnectionMode.MsmtListen"/> (the parent connects instead).</summary>
    ConnectionPoint? ParentPoint { get; }
    /// <summary>
    /// The server topology a <see cref="UserRole.Server"/> instance routes with, keyed by server user name
    /// (case-insensitive): every server in the cluster, not just the local one, and the children each owns. It
    /// says who belongs where, not how to reach them, so a connection is matched to a server or child by the identity
    /// <see cref="IdentifyConnection"/> gives it. Unused outside <see cref="UserRole.Server"/>.
    /// </summary>
    IReadOnlyDictionary<string, ServerUserConfig> Servers { get; }

    /// <summary>Gets the processor that carries out the initial packet exchange on each new connection (see <see cref="IPacketBuilder{TPacket}.InitialProcessor"/>), or <see langword="null"/> for none. Requires <see cref="PacketType"/>.</summary>
    IInitialProcessor? InitialPacketProcessor { get; }
    /// <summary>Gets the processor that carries out the initial message exchange on each new connection (see <see cref="IFrameBuilder{TFrame}.InitialProcessor"/>), or <see langword="null"/> for none.</summary>
    IInitialProcessor? InitialFrameProcessor { get; }
    /// <summary>When <see langword="true"/>, the <c>--config</c> and <c>--user</c> command-line arguments override where the network configuration file and the running user come from (see <see cref="IEngineBuilder.CommandLineOverrides"/>); when <see langword="false"/> (the default) they are ignored and only <c>Config.json</c> and <c>User.json</c> in the working directory are used.</summary>
    bool CommandLineOverridesAllowed { get; }

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

    /// <summary>The processor that reacts to a user connecting or disconnecting and to a message being received (see <see cref="IFrameBuilder{TFrame}.Processor"/>), or <see langword="null"/> for none.</summary>
    INetworkHandler? NetworkHandler { get; }

    /// <summary>Every custom export format added via <see cref="IEngineBuilder.ExportFormat{TFormat}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<ExportFormatDefinition> ExportFormats { get; }

    /// <summary>Every custom import format added via <see cref="IEngineBuilder.ImportFormat{TFormat}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<ImportFormatDefinition> ImportFormats { get; }

    /// <summary>The server users, from <see cref="Servers"/>, that keep a copy of every message they route and answer retrieval requests; see <see cref="UserInfo.StoresMessages"/>. Empty if none.</summary>
    IReadOnlyList<string> StorageServers { get; }

    /// <summary>Every custom auto forward controller added via <see cref="IFrameBuilder{TFrame}.AutoForward{TController}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers { get; }

    /// <summary>Creates a new, empty instance of <see cref="FrameType"/>.</summary>
    object CreateFrame();
    /// <summary>Gets the application-level frame identifier from <paramref name="frame"/>.</summary>
    string GetFrameId(object frame);
    /// <summary>Sets the application-level frame identifier on <paramref name="frame"/>.</summary>
    void SetFrameId(object frame, string value);
    /// <summary>Gets the sender user name from <paramref name="frame"/>.</summary>
    string GetFromUser(object frame);
    /// <summary>Sets the sender user name on <paramref name="frame"/>.</summary>
    void SetFromUser(object frame, string value);
    /// <summary>Gets the body text from <paramref name="frame"/>.</summary>
    string GetBody(object frame);
    /// <summary>Gets the recipient address list from <paramref name="frame"/>.</summary>
    List<MessageAddress> GetAddresses(object frame);
    /// <summary>Sets the recipient address list on <paramref name="frame"/>.</summary>
    void SetAddresses(object frame, List<MessageAddress> value);
    /// <summary>Gets the UTC sent timestamp from <paramref name="frame"/>.</summary>
    DateTime GetSentAt(object frame);
    /// <summary>
    /// Gets the message ID this frame is a read receipt for, or an empty string if
    /// <paramref name="frame"/> is not a read receipt. A read receipt frame carries only this field
    /// (plus <see cref="GetFrameId"/>/<see cref="GetFromUser"/> for its own transport) — body,
    /// and addresses are left unset — and is sent back to the original sender when the recipient opens the
    /// referenced message, so the sender can advance that message's delivery status to <c>Read</c>. See
    /// <c>Docs/Components/Peer.md</c>.
    /// </summary>
    string GetReadReceiptMessageId(object frame);
    /// <summary>Gets whether <paramref name="frame"/> is a read receipt.</summary>
    bool IsReadReceipt(object frame);
    /// <summary>Gets the message ID <paramref name="frame"/> is a receive receipt for, or an empty string if it is not one. It is sent back to the original sender as soon as this node receives the message, so the sender can advance that message's delivery status to <c>Received</c>.</summary>
    string GetReceiveReceiptMessageId(object frame);
    /// <summary>Gets whether <paramref name="frame"/> is a receive receipt.</summary>
    bool IsReceiveReceipt(object frame);
    /// <summary>Gets whether <paramref name="frame"/> is a retrieval request; see <see cref="IFrameBuilder{TFrame}.Retrieval{THandler}"/>.</summary>
    bool IsRetrieval(object frame);
    /// <summary>Reads the criteria a retrieval request carries from its mapped fields. Meaningful only when <see cref="IsRetrieval"/> is <see langword="true"/>.</summary>
    RetrievalCriteria GetRetrieval(object frame);
    /// <summary>Creates a message frame carrying <paramref name="context"/> through the host's message handler, with no identifier, sender, addresses or sent time yet.</summary>
    object CreateMessage(MessageCreateContext context);
    /// <summary>Creates a read receipt frame for the message <paramref name="messageId"/> through the host's read receipt handler, with no identifier, sender, addresses or sent time yet.</summary>
    object CreateReadReceipt(string messageId);
    /// <summary>Creates a receive receipt frame for the message <paramref name="messageId"/> through the host's receive receipt handler, with no identifier, sender, addresses or sent time yet.</summary>
    object CreateReceiveReceipt(string messageId);
    /// <summary>Creates a retrieval request frame asking for <paramref name="criteria"/> through the host's retrieval handler, with no identifier, sender, addresses or sent time yet.</summary>
    object CreateRetrieval(RetrievalCriteria criteria);
    /// <summary>Gets whether <paramref name="frame"/> is a message, one the user reads and that is stored, as opposed to a frame that is only network traffic.</summary>
    bool IsMessage(object frame);
    /// <summary>
    /// Gets whether <paramref name="frame"/> is an alert: an ordinary message that also causes the
    /// receiving Client-mode UI to alarm (visually and audibly) until the user reads it. See
    /// <c>Docs/Components/ViewModels.md</c>.
    /// </summary>
    bool GetIsAlert(object frame);
    /// <summary>
    /// Gets the priority number of <paramref name="frame"/>. One of the values returned by
    /// <see cref="Priorities"/>; used verbatim as the MSMT send priority (larger values are sent first —
    /// see <c>Docs/Components/Peer.md</c>) whenever this frame is sent over an MSMT connection.
    /// </summary>
    int GetPriority(object frame);
    /// <summary>
    /// Gets the short, user-inputted tag identifying the type of message this frame is, or an empty string if
    /// none was set. See <see cref="BlockedCombinations"/>.
    /// </summary>
    string GetTag(object frame);
    /// <summary>
    /// Gets the security level name <paramref name="frame"/> was sent at, one of <see cref="SecurityLevels"/>, or
    /// an empty string when no security levels are configured.
    /// </summary>
    string GetSecurityLevel(object frame);

    /// <summary>Creates a frame packet carrying <paramref name="context"/> through the host's frame packet handler. Only called while <see cref="PacketType"/> is set.</summary>
    object CreateFramePacket(FramePacketCreateContext context);
    /// <summary>Gets whether <paramref name="packet"/> is a frame packet, one that carries a piece of a frame's payload.</summary>
    bool IsFramePacket(object packet);
    /// <summary>Gets the identifier shared by every packet of one payload, which tells packets of different payloads apart.</summary>
    int GetPayloadId(object packet);
    /// <summary>Gets the zero-based position of <paramref name="packet"/> among the packets of its payload.</summary>
    int GetPacketIndex(object packet);
    /// <summary>Gets how many packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    int GetPacketCount(object packet);
    /// <summary>Gets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    int GetPayloadLength(object packet);
    /// <summary>Gets the slice of the payload <paramref name="packet"/> carries.</summary>
    ReadOnlyMemory<byte> GetPacketData(object packet);

    /// <summary>Resolves <paramref name="userCode"/> to the name of the user it installs, or <see langword="null"/> if the code is unrecognized. Unless the host states its own scheme (<see cref="IEngineBuilder.UserCodes"/>), a code is the name of a user of the network.</summary>
    /// <param name="userCode">The user installation code to resolve.</param>
    string? ResolveUserName(string userCode);
    /// <summary>
    /// Returns what is known about <paramref name="userName"/>: what the network configuration file states (see <see cref="IEngineBuilder.CommandLineOverrides"/>), or a user with just
    /// that name when it states none. For the current user this is where <see cref="Role"/>, <see cref="PeerPort"/>, <see cref="InterfacePort"/>,
    /// <see cref="OutgoingPoints"/> and <see cref="Servers"/> come from.
    /// </summary>
    /// <param name="userName">The user to describe.</param>
    UserInfo GetUserInfo(string userName);
    /// <summary>
    /// Returns the app-specific information attached to <paramref name="userName"/>, an empty map when there is none. The
    /// engine does not interpret it: it travels with the user's <see cref="UserIdentity"/>, so a host can attach whatever
    /// it needs to a user (a role, a station, a display name) and read it back wherever the user is identified.
    /// </summary>
    /// <param name="userName">The user to describe.</param>
    IReadOnlyDictionary<string, string> GetUserData(string userName);

    /// <summary>
    /// Decides who is on the other end of a connection that has just formed, from what is known about it: for IP the
    /// remote host, port and certificate names, for serial the port and addresses, and the initial packet and
    /// message exchange when those are configured. Returns <see langword="null"/> (the default) to let the engine decide:
    /// an IP connection is the user whose <see cref="GetCertificateName"/> matches a name in its certificate (or, when
    /// none does, a user named after that certificate name), and a serial connection is a user named after its port.
    /// A host overrides this to identify by anything else, such as a serial port to user table, or a user name carried
    /// in an initial packet or message. The identity built from the returned name carries <see cref="GetUserData"/> for it.
    /// </summary>
    /// <param name="connection">What is known about the connection.</param>
    string? IdentifyConnection(IConnectionInfo connection);

    /// <summary>Adds what the engine knows about this node, namely which user it runs as, to a description of a new connection before it is handed to a host's processor.</summary>
    /// <param name="connection">What is known about the connection.</param>
    IConnectionInfo WithLocalUser(IConnectionInfo connection);

    /// <summary>
    /// Returns how many times <paramref name="message"/> should be automatically added to the print queue
    /// when it arrives — <c>0</c> to not print it, <c>1</c> to print it once, <c>2</c> to print two copies, and
    /// so on. Only consulted while the print manager's "print received" toggle is enabled.
    /// </summary>
    /// <param name="message">The received message, in this instance's own <see cref="FrameType"/>.</param>
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
/// mappings were turned into. the network file's node settings are applied on top by <see cref="ConfiguredEngineController"/>. Not
/// sealed and every member is virtual so tests can replace a single behavior.
/// </summary>
/// <param name="builder">What the host stated.</param>
/// <param name="currentUserProvider">Tracks the user name of the currently running instance, read for <see cref="ConnectionOptions"/>.</param>
/// <param name="networkConfig">The network configuration file describing every user of the network; empty when none is loaded.</param>
/// <param name="services">The running engine's container, which instantiates the host's processors; <see langword="null"/> for one with no services.</param>
internal class EngineController(EngineBuilder builder, ICurrentUserProvider currentUserProvider, NetworkConfig? networkConfig = null, IServiceProvider? services = null) : IEngineController
{
    private static IReadOnlyList<TDefinition> Replacing<TFormat, TDefinition>(IEnumerable<TFormat> formats, Func<TFormat, TDefinition> define, Func<TDefinition, string> name)
    {
        List<TDefinition> definitions = [];
        foreach (TDefinition definition in formats.Select(define))
        {
            int existing = definitions.FindIndex(other => string.Equals(name(other), name(definition), StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) { definitions[existing] = definition; }
            else { definitions.Add(definition); }
        }

        return definitions;
    }

    private readonly FrameMap frame = builder.FrameMap ?? throw new InvalidOperationException("The engine configuration must state its frame type with Frames<TFrame>(...).");
    private readonly PacketMap? packet = builder.PacketMap;
    private readonly Lazy<IMessageFrameHandler> messageHandler = new(() => builder.FrameMap!.Message.Create(services));
    private readonly Lazy<IRetrievalFrameHandler> retrievalHandler = new(() => builder.FrameMap!.Retrieval.Create(services));
    private readonly Lazy<IReceiptFrameHandler> readReceiptHandler = new(() => builder.FrameMap!.ReadReceipt.Create(services));
    private readonly Lazy<IReceiptFrameHandler> receiveReceiptHandler = new(() => builder.FrameMap!.ReceiveReceipt.Create(services));
    private readonly Lazy<IFramePacketAdapter> framePacketHandler = new(() => (builder.PacketMap ?? throw new NotSupportedException("This engine has no packet type; state one with Packets<TPacket>(...) to enable packetization.")).FramePacket.Create(services));
    private readonly Lazy<IFrameSerializer> frameSerializer = new(() => (builder.FrameMap ?? throw new InvalidOperationException("The engine configuration must state its frame type with Frames<TFrame>(...).")).Serializer.Create(services));
    private readonly Lazy<IPacketSerializer?> packetSerializer = new(() => builder.PacketMap?.Serializer.Create(services));
    private readonly Lazy<IReadOnlyList<ExportFormatDefinition>> exportFormats = new(() => Replacing(
        builder.ExportFormats.Select(registration => registration.Create(services)),
        format => new ExportFormatDefinition { Name = format.Name, Serialize = format.Export, AllowedTypes = format.Accepts },
        definition => definition.Name));
    private readonly Lazy<IReadOnlyList<ImportFormatDefinition>> importFormats = new(() => Replacing(
        builder.ImportFormats.Select(registration => registration.Create(services)),
        format => new ImportFormatDefinition { Name = format.Name, Read = format.Import, StagedSendMode = format.StagedSendMode, StagedSendDelay = format.StagedSendDelay },
        definition => definition.Name));
    private readonly Lazy<IReadOnlyList<AutoForwardControllerDefinition>> autoForwardControllers = new(() => Replacing(
        builder.AutoForwardControllers.Select(registration => registration.Create(services)),
        definition => definition,
        definition => definition.Name));
    private readonly Lazy<IInitialProcessor?> initialPacketProcessor = new(() => builder.InitialPacketProcessor?.Create(services));
    private readonly Lazy<IInitialProcessor?> initialMessageProcessor = new(() => builder.InitialFrameProcessor?.Create(services));
    private readonly Lazy<INetworkHandler?> networkHandler = new(() => builder.NetworkHandler?.Create(services));
    private readonly IReadOnlyList<MessagePriorityOption> defaultPriorities = [new MessagePriorityOption { Name = "Normal", Value = 0 }];
    private readonly IReadOnlyList<AddressType> addressTypeOrder = [AddressType.To, AddressType.Cc, AddressType.External];

    /// <inheritdoc />
    public virtual Type FrameType => frame.Type;
    /// <inheritdoc />
    public virtual IFrameSerializer FrameSerializer => frameSerializer.Value;
    /// <inheritdoc />
    public virtual Type? PacketType => packet?.Type;
    /// <inheritdoc />
    public virtual IPacketSerializer? PacketSerializer => packetSerializer.Value;
    /// <inheritdoc />
    public virtual int PacketSize => packet?.Size ?? 16 * 1024;
    /// <inheritdoc />
    public virtual int PacketWindow => packet?.Window ?? 1;

    /// <inheritdoc />
    public virtual string AppName => builder.AppNameValue ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <inheritdoc />
    public virtual string AppVersion => builder.AppVersionValue ?? (Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0");
    /// <inheritdoc />
    public virtual string AppDataRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    /// <inheritdoc />
    public virtual string AppDataPath => currentUserProvider.UserName is { Length: > 0 } user ? Path.Combine(AppDataRoot, AppName, user) : Path.Combine(AppDataRoot, AppName);
    /// <inheritdoc />
    public virtual string StatePath => Path.Combine(AppDataRoot, AppName, "State.json");
    /// <inheritdoc />
    public virtual bool IsKioskMode => builder.IsKioskMode;
    /// <inheritdoc />
    public virtual string HomeText => builder.HomeTextValue ?? "HOME";
    private readonly NetworkConfig network = networkConfig ?? new();

    private UserInfo? CurrentUserInfo => currentUserProvider.UserName is { Length: > 0 } name ? GetUserInfo(name) : null;

    /// <inheritdoc />
    public virtual string? WindowIconPath => builder.WindowIconValue;

    /// <inheritdoc />
    public virtual string? DebugUserName => builder.DebugUserValue;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> Users
    {
        get
        {
            List<string> names = [.. builder.UserNames];
            foreach (string name in network.Users.Keys.Concat(UserGroups.Keys))
            {
                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase)) { names.Add(name); }
            }
            return names;
        }
    }
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups
    {
        get
        {
            Dictionary<string, IReadOnlyList<string>> merged = new(builder.UserGroups, StringComparer.OrdinalIgnoreCase);
            foreach ((string name, List<string> members) in network.UserGroups) { merged[name] = members; }
            return merged;
        }
    }

    /// <inheritdoc />
    public virtual int PeerPort => CurrentUserInfo?.PeerPoint?.Port ?? new PeerPoint().Port;
    /// <inheritdoc />
    public virtual int InterfacePort => CurrentUserInfo?.InterfacePort ?? 50020;

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
        if (GetUserInfo(userName).SecurityLevel is { } stated) { return stated; }
        return builder.SecurityLevelValues.Count > 0 ? builder.SecurityLevelValues[0].Name : string.Empty;
    }

    /// <inheritdoc />
    public virtual bool PrintReceivedDefaultEnabled => builder.PrintReceivedValue ?? false;

    /// <inheritdoc />
    public virtual MsmtSessionPeerOptions ConnectionOptions => ConfigureConnectionOptions(builder.ConnectionOptionsValue?.Invoke()
        ?? MsmtCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName, TrustedAuthorityCertificateName));

    /// <inheritdoc />
    public virtual MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options) => builder.MsmtOptionsValue is { } stated
        ? options with
        {
            HandshakeTimeout = stated.HandshakeTimeout,
            StallTimeout = stated.StallTimeout,
            ResponseTimeout = stated.ResponseTimeout,
            TcpKeepAliveTime = stated.TcpKeepAliveTime,
            MaximumSessionLifetime = stated.MaximumSessionLifetime,
            SessionLifetime = stated.SessionLifetime,
            KeepAliveMinInterval = stated.KeepAliveMinInterval,
            KeepAliveMaxInterval = stated.KeepAliveMaxInterval
        }
        : options;

    /// <inheritdoc />
    public virtual MicroGatePeerOptions MicroGateOptions => builder.MicroGateOptionsValue ?? new();

    /// <inheritdoc />
    public virtual UserRole Role => CurrentUserInfo?.Role ?? UserRole.Peer;
    /// <inheritdoc />
    public virtual IReadOnlyList<ConnectionPoint> OutgoingPoints
    {
        get
        {
            if (CurrentUserInfo is not { } current) { return []; }

            List<ConnectionPoint> points = [];
            if (current.Parent is { } parent && DialPoint(parent, isParent: true) is { } parentPoint) { points.Add(parentPoint); }
            points.AddRange(current.Children.Select(child => DialPoint(child, isParent: false)).OfType<ConnectionPoint>());
            if ((current.Role ?? UserRole.Peer) == UserRole.Peer)
            {
                points.AddRange(Users
                    .Select(GetUserInfo)
                    .Where(other => (other.Role ?? UserRole.Peer) == UserRole.Peer && other.PeerPoint is not null && !string.Equals(other.Name, current.Name, StringComparison.OrdinalIgnoreCase)
                        && (current.PeerPoint is null || string.Compare(current.Name, other.Name, StringComparison.OrdinalIgnoreCase) < 0))
                    .Select(other => new ConnectionPoint { IpAddress = other.PeerPoint!.Host, Port = other.PeerPoint.Port }));
            }

            return [.. points.DistinctBy(point => point.Key)];
        }
    }
    /// <inheritdoc />
    public virtual string? ParentUser => CurrentUserInfo?.Parent?.User;
    /// <inheritdoc />
    public virtual ConnectionPoint? ParentPoint => CurrentUserInfo?.Parent is { } parent ? DialPoint(parent, isParent: true) : null;

    private static ConnectionMode ModeOf(UserLink link, bool isParent)
        => link.Mode ?? (!string.IsNullOrEmpty(link.SerialPort) ? ConnectionMode.SyncSerial : isParent ? ConnectionMode.MsmtConnect : ConnectionMode.MsmtListen);

    private ConnectionPoint? DialPoint(UserLink link, bool isParent)
        => ModeOf(link, isParent) switch
        {
            ConnectionMode.MsmtConnect => GetUserInfo(link.User).PeerPoint is { } point
                ? new ConnectionPoint { IpAddress = point.Host, Port = point.Port }
                : new ConnectionPoint { IpAddress = new PeerPoint().Host, Port = new PeerPoint().Port },
            ConnectionMode.SyncSerial when !string.IsNullOrEmpty(link.SerialPort) => new ConnectionPoint { SerialPort = link.SerialPort, SerialAddress = link.SerialAddress, RemoteSerialAddress = link.RemoteSerialAddress, User = link.User },
            _ => null
        };
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, ServerUserConfig> Servers
    {
        get
        {
            Dictionary<string, ServerUserConfig> servers = new(StringComparer.OrdinalIgnoreCase);
            foreach (string name in Users)
            {
                if (GetUserInfo(name) is { Role: UserRole.Server } info) { servers[name] = BuildServerConfig(info); }
            }
            if (CurrentUserInfo is { Role: UserRole.Server } current) { servers[current.Name] = BuildServerConfig(current); }
            return servers;
        }
    }

    private ServerUserConfig BuildServerConfig(UserInfo server)
        => new()
        {
            Children = [.. server.Children.Select(child => child.User)],
            Relays = server.Children
                .Where(child => GetUserInfo(child.User) is { Role: UserRole.Relay })
                .ToDictionary(child => child.User, child => (IReadOnlyList<string>)[.. GetUserInfo(child.User).Children.Select(link => link.User)], StringComparer.OrdinalIgnoreCase)
        };

    /// <inheritdoc />
    public virtual IInitialProcessor? InitialPacketProcessor => initialPacketProcessor.Value;

    /// <inheritdoc />
    public virtual IInitialProcessor? InitialFrameProcessor => initialMessageProcessor.Value;

    /// <inheritdoc />
    public virtual bool CommandLineOverridesAllowed => builder.AreCommandLineOverridesAllowed;

    /// <inheritdoc />
    public virtual IReadOnlyList<IExternalSystem> ExternalSystems => builder.ExternalSystems;
    /// <inheritdoc />
    public virtual IExternalSystem? ExternalServer => builder.ExternalServerValue;

    /// <inheritdoc />
    public virtual INetworkHandler? NetworkHandler => networkHandler.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<ExportFormatDefinition> ExportFormats => exportFormats.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<ImportFormatDefinition> ImportFormats => importFormats.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> StorageServers => [.. Servers.Keys.Where(name => GetUserInfo(name).StoresMessages)];
    /// <inheritdoc />
    public virtual IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers => autoForwardControllers.Value;

    /// <inheritdoc />
    public virtual string TrustedAuthorityCertificateName => network.TrustedAuthorityCertificateName ?? builder.TrustedAuthorityValue ?? "COMLINK-ROOT";

    /// <inheritdoc />
    public virtual object CreateFrame() => frame.Create();
    /// <inheritdoc />
    public virtual string GetFrameId(object value) => frame.GetId(value);
    /// <inheritdoc />
    public virtual void SetFrameId(object value, string id) => frame.SetId(value, id);
    /// <inheritdoc />
    public virtual string GetFromUser(object value) => frame.GetSender(value);
    /// <inheritdoc />
    public virtual void SetFromUser(object value, string user) => frame.SetSender(value, user);
    /// <inheritdoc />
    public virtual List<MessageAddress> GetAddresses(object value) => frame.GetAddresses(value);
    /// <inheritdoc />
    public virtual void SetAddresses(object value, List<MessageAddress> addresses) => frame.SetAddresses(value, addresses);
    /// <inheritdoc />
    public virtual DateTime GetSentAt(object value) => messageHandler.Value.GetSentAt(value);
    /// <inheritdoc />
    public virtual bool IsMessage(object value) => messageHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateMessage(MessageCreateContext context) => messageHandler.Value.Create(context);
    /// <inheritdoc />
    public virtual string GetBody(object value) => messageHandler.Value.GetBody(value);
    /// <inheritdoc />
    public virtual bool GetIsAlert(object value) => messageHandler.Value.GetIsAlert(value);
    /// <inheritdoc />
    public virtual int GetPriority(object value) => messageHandler.Value.GetPriority(value);
    /// <inheritdoc />
    public virtual string GetTag(object value) => messageHandler.Value.GetTag(value);
    /// <inheritdoc />
    public virtual string GetSecurityLevel(object value) => messageHandler.Value.GetSecurityLevel(value);
    /// <inheritdoc />
    public virtual bool IsReadReceipt(object value) => readReceiptHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateReadReceipt(string messageId) => readReceiptHandler.Value.Create(new ReceiptCreateContext { MessageId = messageId });
    /// <inheritdoc />
    public virtual string GetReadReceiptMessageId(object value) => readReceiptHandler.Value.GetMessageId(value);
    /// <inheritdoc />
    public virtual bool IsReceiveReceipt(object value) => receiveReceiptHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateReceiveReceipt(string messageId) => receiveReceiptHandler.Value.Create(new ReceiptCreateContext { MessageId = messageId });
    /// <inheritdoc />
    public virtual string GetReceiveReceiptMessageId(object value) => receiveReceiptHandler.Value.GetMessageId(value);
    /// <inheritdoc />
    public virtual bool IsRetrieval(object value) => retrievalHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateRetrieval(RetrievalCriteria criteria) => retrievalHandler.Value.Create(new RetrievalCreateContext
    {
        From = criteria.From,
        To = criteria.To,
        Authors = criteria.Authors,
        Destinations = criteria.Destinations,
        Ids = criteria.Ids
    });
    /// <inheritdoc />
    public virtual RetrievalCriteria GetRetrieval(object value) => new()
    {
        From = retrievalHandler.Value.GetFrom(value),
        To = retrievalHandler.Value.GetTo(value),
        Authors = [.. retrievalHandler.Value.GetAuthors(value)],
        Destinations = [.. retrievalHandler.Value.GetDestinations(value)],
        Ids = [.. retrievalHandler.Value.GetIds(value)]
    };

    /// <inheritdoc />
    public virtual object CreateFramePacket(FramePacketCreateContext context) => framePacketHandler.Value.Create(context);
    /// <inheritdoc />
    public virtual bool IsFramePacket(object value) => framePacketHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual int GetPayloadId(object value) => framePacketHandler.Value.GetPayloadId(value);
    /// <inheritdoc />
    public virtual int GetPacketIndex(object value) => framePacketHandler.Value.GetIndex(value);
    /// <inheritdoc />
    public virtual int GetPacketCount(object value) => framePacketHandler.Value.GetCount(value);
    /// <inheritdoc />
    public virtual int GetPayloadLength(object value) => framePacketHandler.Value.GetPayloadLength(value);
    /// <inheritdoc />
    public virtual ReadOnlyMemory<byte> GetPacketData(object value) => framePacketHandler.Value.GetData(value);

    /// <inheritdoc />
    public virtual string? ResolveUserName(string userCode)
    {
        if (builder.UserCodeResolver is { } resolve) { return resolve(userCode); }

        string? user = builder.UserNames.Concat(network.Users.Keys).FirstOrDefault(name => string.Equals(name, userCode, StringComparison.OrdinalIgnoreCase));
        return user ?? (userCode.Equals("CODE", StringComparison.OrdinalIgnoreCase) ? "TEST" : null);
    }

    /// <inheritdoc />
    public virtual UserInfo GetUserInfo(string userName)
    {
        UserInfo info = network.GetUserInfo(userName) ?? new UserInfo { Name = userName };
        return info with { Groups = [.. UserGroups.Where(group => group.Value.Contains(userName, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)] };
    }

    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, string> GetUserData(string userName) => GetUserInfo(userName).Data;

    /// <inheritdoc />
    public virtual string? IdentifyConnection(IConnectionInfo connection) => builder.IdentifyValue?.Invoke(connection);
    /// <inheritdoc />
    public virtual IConnectionInfo WithLocalUser(IConnectionInfo connection) => connection;

    /// <inheritdoc />
    public virtual int GetPrintCount(object value) => builder.PrintCountValue?.Invoke(value) ?? 1;

    /// <inheritdoc />
    public virtual bool CanDelete(FolderType folderType) => builder.CanDeleteValue?.Invoke(folderType) ?? true;

    /// <inheritdoc />
    public virtual string GetCertificateName(string userName) => GetUserInfo(userName).CertificateName ?? userName;

    private PacketMap Packet => packet ?? throw new NotSupportedException("This engine has no packet type; state one with Packets<TPacket>(...) to enable packetization.");
}

/// <summary>Extension members for <see cref="IEngineController"/>.</summary>
internal static class EngineControllerExtensions
{
    extension(IEngineController engineController)
    {
        /// <summary>
        /// The message a node sends to keep a connection verified: an empty instance of <see cref="IEngineController.FrameType"/>, serialized like any
        /// message. It is recognized by having no identifier, addresses, receipt or retrieval, which no message built by the engine ever lacks,
        /// and is acknowledged and otherwise ignored.
        /// </summary>
        /// <param name="message">A received instance of <see cref="IEngineController.FrameType"/>.</param>
        public bool IsHeartbeat(object message)
            => string.IsNullOrEmpty(engineController.GetFrameId(message))
            && engineController.GetAddresses(message).Count == 0
            && !engineController.IsReadReceipt(message)
            && !engineController.IsReceiveReceipt(message)
            && !engineController.IsRetrieval(message);

        /// <summary>
        /// Reads every logical field of <paramref name="payload"/> (an instance of <see cref="IEngineController.FrameType"/>)
        /// into a new <see cref="MessageReceivedEvent"/>. Shared by <see cref="DirectServiceConnection"/> and
        /// <see cref="EngineHooksService"/>, so both surface the exact same fields for an inbound message.
        /// </summary>
        public MessageReceivedEvent ToMessageReceivedEvent(object payload) => new()
        {
            MessageId = engineController.GetFrameId(payload),
            FromUser = engineController.GetFromUser(payload),
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
