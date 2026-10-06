namespace BlueHeighliner.Comlink;

/// <summary>
/// Single control interface consolidating every extension point through which a host application
/// customises Engine behaviour without modifying Engine code: the concrete frame type and its logical
/// field mapping, how that frame type is serialized and packetized (and at what packet size and window) for the network, app
/// identity/presentation, local user identity, the user/group directory, listener ports, alert settings,
/// message composition, the automatic print policy, MSMT peer certificate naming and peer options, network
/// topology, the points this node connects out to, how the user on the other end of a connection is identified
/// (optionally after an initial packet and message exchange), the external systems this instance communicates with, the
/// hooks run on connection and message activity, and whether command-line arguments may override the network configuration file and user. External drive discovery and printer discovery/driving are real
/// OS-level behavior, not configuration or rules, so they live on <see cref="IExternalDriveProvider"/>
/// and <see cref="IPrintDriver"/> instead. See <c>Docs/Components/Configuration.md</c>.
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
    /// Absolute path to the file remembering which user is installed, <c>{AppDataRoot}/{AppName}/User.json</c>. It lives beside the user
    /// folders rather than in one, since it is what says whose folder to use, and holds nothing else, so deleting it only uninstalls the user.
    /// </summary>
    string UserFilePath { get; }
    /// <summary><see langword="true"/> to enable kiosk mode, which hides the minimize and maximize buttons and has the close button restart rather than exit.</summary>
    bool IsKioskMode { get; }
    /// <summary>Whether alert messages are kept in their own alert inbox and alert outbox, apart from the normal inbox and outbox, from the display handler.</summary>
    bool SeparateAlerts { get; }
    /// <summary>The label the network indicator shows while online (<paramref name="isOnline"/> is <see langword="true"/>) or offline, from the display handler.</summary>
    /// <param name="isOnline">Whether the indicator shows online.</param>
    string GetNetworkIndicatorLabel(bool isOnline);
    /// <summary>The hex color the network indicator shows while online or offline, from the display handler.</summary>
    /// <param name="isOnline">Whether the indicator shows online.</param>
    string GetNetworkIndicatorColor(bool isOnline);
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

    /// <summary>Port for the inbound IP listener: the MSMT port of the current user (see <see cref="UserInfo.MsmtPort"/>), 50021 by default.</summary>
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
    /// <summary>How long the alarm sound plays after a connection drops (see <see cref="IDisconnectAlarmService"/>), from the alarm handler.</summary>
    TimeSpan DisconnectAlarmDuration { get; }
    /// <summary>
    /// The names of the keys that, pressed while focus is not in a text input, open the oldest unread alert, from the message handler.
    /// </summary>
    IReadOnlyList<string> AlertQuickReadKeys { get; }
    /// <summary>
    /// Returns whether the received message <paramref name="message"/> is kept. A message that is not an alert always is. An alert is put to the message handler's <c>FilterAlerts</c> with the alerts kept before it; one that is kept is added
    /// to that history, dropping the oldest once it holds <c>AlertHistoryLimit</c> of them, and one that is not is to be thrown away.
    /// </summary>
    /// <param name="message">An instance of <see cref="FrameType"/> that was received.</param>
    bool AcceptAlert(object message);
    /// <summary>Gets how wide a line of a draft may be (see <see cref="IDraftHandler{TPriority, TLevel}"/>), or <see langword="null"/> when the draft view does not offer a width, which is without a draft handler or when it states neither a default nor a maximum.</summary>
    LineWidthRange? DraftLineWidth { get; }
    /// <summary>Gets what message tags may be: their case, length, and whether they may hold symbols, numbers and spaces (see <see cref="IDraftHandler{TPriority, TLevel}"/>). Unrestricted without a draft handler.</summary>
    TagRules DraftTagRules { get; }
    /// <summary>Gets what a new draft starts with: its tag, priority and security level (see <see cref="IDraftHandler{TPriority, TLevel}"/>).</summary>
    DraftDefaults DraftDefaults { get; }
    /// <summary>Returns the header a message sent from the draft described by <paramref name="draft"/> must start with, or <see langword="null"/> for none (always the case without a draft handler).</summary>
    /// <param name="draft">The draft as it currently is.</param>
    string? GetDraftHeader(DraftContent draft);

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
    /// <summary>Gets the name of the priority concept in the user interface (see <see cref="IDisplayHandler.PriorityLabel"/>).</summary>
    string PriorityLabel { get; }
    /// <summary>Gets the name of the security level concept in the user interface (see <see cref="IDisplayHandler.SecurityLevelLabel"/>).</summary>
    string SecurityLevelLabel { get; }
    /// <summary>Gets the plural of <see cref="AlertLabel"/>.</summary>
    string AlertPluralLabel { get; }
    /// <summary>Gets the plural of <see cref="TagLabel"/>.</summary>
    string TagPluralLabel { get; }
    /// <summary>Gets the plural of <see cref="PriorityLabel"/>.</summary>
    string PriorityPluralLabel { get; }
    /// <summary>Gets the plural of <see cref="SecurityLevelLabel"/>.</summary>
    string SecurityLevelPluralLabel { get; }
    /// <summary>Returns the text to show for <paramref name="label"/>, the engine's own name for a concept of the app: what the host's display handler calls it (see <see cref="IDisplayHandler.InboxLabel"/> and the members like it), or <paramref name="label"/> itself.</summary>
    /// <param name="label">The engine's name for the concept.</param>
    string Rename(string label);
    /// <summary>Every blocked tag/priority combination rule, enforced when composing a draft.</summary>
    IReadOnlyList<TagPriorityBlock> BlockedCombinations { get; }
    /// <summary>
    /// Every address type, in a fixed order (<see cref="AddressType.To"/>, <see cref="AddressType.Cc"/>,
    /// <see cref="AddressType.External"/>), paired with its display label - shown in the address type picker, the
    /// per-address badge, and the message view's section headers. A label defaults to the enum name unless overridden
    /// with <see cref="IAddressTypeBuilder{TFrame, TPacket, TPriority, TLevel}.Label"/>.
    /// </summary>
    IReadOnlyList<AddressTypeOption> AddressTypes { get; }
    /// <summary>
    /// Every configured security level, in ascending order (index 0 is lowest); empty when
    /// <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.SecurityLevels"/> was never stated, which turns the whole feature off. A
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
    /// When <see langword="true"/>, the print manager's "print received" toggle (<see cref="IPrintManagerViewModel.PrintReceivedEnabled"/>)
    /// starts enabled, so every received message is automatically added to the print queue from the moment
    /// the app starts. The user can still toggle it off at any time.
    /// </summary>
    bool PrintReceivedDefaultEnabled { get; }

    /// <summary>The peer options - including TLS identity certificate and trusted certificate authorities - used for both inbound and outbound MSMT session peer connections.</summary>
    MsmtSessionPeerOptions ConnectionOptions { get; }

    /// <summary>Applies the host's adjustment of the MSMT options (see <see cref="IConnectionsBuilder{TFrame, TPacket, TPriority, TLevel}.Msmt(MsmtConnectionOptions)"/>) to <paramref name="options"/>, returning them unchanged if none was stated.</summary>
    MsmtSessionPeerOptions ConfigureConnectionOptions(MsmtSessionPeerOptions options);

    /// <summary>The options used for every MicroGate serial connection, after the host's adjustment (see <see cref="IConnectionsBuilder{TFrame, TPacket, TPriority, TLevel}.Hdlc(HdlcPeerOptions)"/>).</summary>
    HdlcPeerOptions HdlcOptions { get; }

    /// <summary>The configured role for this instance.</summary>
    UserRole Role { get; }
    /// <summary>
    /// The points this node connects out to, and keeps connected, worked out from its links: for each link to its parent or a child whose mode is
    /// <see cref="ConnectionMode.MsmtConnect"/>, the other user's <see cref="UserInfo.IpHost"/> and <see cref="UserInfo.MsmtPort"/>, and for the <see cref="ConnectionMode.Hdlc"/> links together one point per HDLC port
    /// the node opens (a serial cable joins two nodes and is opened from both ends); the parent's comes first.
    /// Who is on the other end of a connection is still worked out
    /// when it forms.
    /// </summary>
    IReadOnlyList<ConnectionPoint> OutgoingPoints { get; }
    /// <summary>The name of the current user's parent, or <see langword="null"/> for none.</summary>
    string? ParentUser { get; }
    /// <summary>The points this node dials to reach its parent: the parent's IP host and MSMT port, or one per HDLC port this node opens. Empty when it has no parent or the parent's link is <see cref="ConnectionMode.MsmtListen"/> (the parent connects instead).</summary>
    IReadOnlyList<ConnectionPoint> ParentPoints { get; }
    /// <summary>
    /// The server topology a <see cref="UserRole.Server"/> instance routes with, keyed by server user name
    /// (case-insensitive): every server in the cluster, not just the local one, and the children each owns. It
    /// says who belongs where, not how to reach them, so a connection is matched to a server or child by the identity
    /// the engine gives it when it forms. Unused outside <see cref="UserRole.Server"/>.
    /// </summary>
    IReadOnlyDictionary<string, ServerUserConfig> Servers { get; }

    /// <summary>Gets the processor that carries out the initial packet exchange on each new connection (see <see cref="IPacketBuilder{TFrame, TPacket, TPriority, TLevel}.InitialProcessor"/>), or <see langword="null"/> for none. Requires <see cref="PacketType"/>.</summary>
    IInitialProcessor? InitialPacketProcessor { get; }
    /// <summary>Gets the processor that carries out the initial message exchange on each new connection (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.InitialProcessor"/>), or <see langword="null"/> for none.</summary>
    IInitialProcessor? InitialFrameProcessor { get; }
    /// <summary>When <see langword="true"/>, the <c>--config</c> and <c>--user</c> command-line arguments override where the network configuration file and the running user come from (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.CommandLineOverrides"/>); when <see langword="false"/> (the default) they are ignored and only <c>Config.json</c> and <c>User.json</c> in the working directory are used.</summary>
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

    /// <summary>The processor that reacts to a user connecting or disconnecting and to a message being received (see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Processor"/>), or <see langword="null"/> for none.</summary>
    INetworkHandler? NetworkHandler { get; }

    /// <summary>Every custom export format added via <see cref="IExportsBuilder{TFrame, TPacket, TPriority, TLevel}.Format{TFormat}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<ExportFormatDefinition> ExportFormats { get; }

    /// <summary>Every custom import format added via <see cref="IImportsBuilder{TFrame, TPacket, TPriority, TLevel}.Format{TFormat}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<ImportFormatDefinition> ImportFormats { get; }

    /// <summary>Every server user, from <see cref="Servers"/>: each keeps a copy of every message one of its own children sends and answers retrieval requests for them, so a retrieval names the server the message is stored on. Empty if none.</summary>
    IReadOnlyList<string> StorageServers { get; }

    /// <summary>Every custom auto forward controller added via <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.AutoForward{TController}"/>, in the order added; empty if none.</summary>
    IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers { get; }

    /// <summary>Creates a new, empty instance of <see cref="FrameType"/>.</summary>
    object CreateFrame();
    /// <summary>Gets the identifier of <paramref name="message"/>, an instance of <see cref="FrameType"/> the message handler recognizes. Only messages have one.</summary>
    string GetMessageId(object message);
    /// <summary>Sets the identifier of <paramref name="message"/>, an instance of <see cref="FrameType"/> the message handler recognizes.</summary>
    void SetMessageId(object message, string id);
    /// <summary>Gets the sender user name from <paramref name="frame"/>, through the handler of its kind (message, retrieval request or receipt); empty for any other frame, which has no sender.</summary>
    string GetFromUser(object frame);
    /// <summary>Sets the sender user name on <paramref name="frame"/>, through the handler of its kind; does nothing for a frame that has no sender.</summary>
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
    /// (plus <see cref="GetMessageId"/>/<see cref="GetFromUser"/> for its own transport) — body,
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
    /// <summary>Gets whether a heartbeat handler is stated. When it is not, no heartbeats are sent and a connection counts as up once it is established.</summary>
    bool HeartbeatsEnabled { get; }
    /// <summary>Gets whether a heartbeat packet handler is stated, in which case heartbeats are sent as packets, beneath packetization, instead of as frames.</summary>
    bool PacketHeartbeatsEnabled { get; }
    /// <summary>Creates a heartbeat packet. Only valid while <see cref="PacketHeartbeatsEnabled"/>.</summary>
    object CreatePacketHeartbeat();
    /// <summary>Returns whether <paramref name="packet"/> is a heartbeat packet, which is discarded; always <see langword="false"/> when no heartbeat packet handler is stated.</summary>
    /// <param name="packet">A received instance of <see cref="PacketType"/>.</param>
    bool IsPacketHeartbeat(object packet);
    /// <summary>Gets the priority heartbeat packets are sent with.</summary>
    int PacketHeartbeatPriority { get; }
    /// <summary>Gets how long a connection waits between heartbeats while they succeed, as stated by the heartbeat handler in use (the packet one first), or 30 seconds when there is none.</summary>
    TimeSpan HeartbeatInterval { get; }
    /// <summary>Gets how long a connection waits between heartbeats while they fail, as stated by the heartbeat handler in use (the packet one first), or 2 seconds when there is none.</summary>
    TimeSpan HeartbeatRetryInterval { get; }
    /// <summary>Creates a heartbeat frame. Only valid while <see cref="HeartbeatsEnabled"/>.</summary>
    object CreateHeartbeat();
    /// <summary>Returns whether <paramref name="frame"/> is a heartbeat, which is acknowledged and otherwise ignored; always <see langword="false"/> when no heartbeat handler is stated.</summary>
    /// <param name="frame">A received instance of <see cref="FrameType"/>.</param>
    bool IsHeartbeat(object frame);
    /// <summary>Gets whether <paramref name="frame"/> is a retrieval request; see <see cref="IFrameBuilder{TFrame, TPacket, TPriority, TLevel}.Retrieval{THandler}"/>.</summary>
    bool IsRetrieval(object frame);
    /// <summary>Reads the criteria a retrieval request carries from its mapped fields. Meaningful only when <see cref="IsRetrieval"/> is <see langword="true"/>.</summary>
    RetrievalCriteria GetRetrieval(object frame);
    /// <summary>Creates a message frame carrying <paramref name="context"/> through the host's message handler, with no identifier, sender, addresses or sent time yet.</summary>
    object CreateMessage(MessageContent context);
    /// <summary>Creates a read receipt frame for the message <paramref name="messageId"/>, which is for the user <paramref name="to"/>, through the host's read receipt handler, with no sender yet.</summary>
    object CreateReadReceipt(string messageId, string to);
    /// <summary>Creates a receive receipt frame for the message <paramref name="messageId"/>, which is for the user <paramref name="to"/>, through the host's receive receipt handler, with no sender yet.</summary>
    object CreateReceiveReceipt(string messageId, string to);
    /// <summary>Creates a retrieval request frame asking <paramref name="server"/> for <paramref name="criteria"/> through the host's retrieval handler, with no sender yet.</summary>
    object CreateRetrieval(RetrievalCriteria criteria, string server);
    /// <summary>Returns the user names <paramref name="frame"/> needs to get to: for a message the users it is addressed to other than external ones (a name may be a group), for a retrieval request or receipt the destination its handler reads, and nobody for any other frame.</summary>
    IReadOnlyList<string> Route(object frame);
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
    /// <see cref="Priorities"/>: a message's own (brought within the configured levels by <see cref="ResolvePriority"/>), or for a retrieval request or receipt the priority its handler names; used as the send priority (larger values are sent first —
    /// see <c>Docs/Components/Peer.md</c>) whenever this frame is sent.
    /// </summary>
    int GetPriority(object frame);
    /// <summary>Generates the identifier of the next message this node creates, through the host's message handler (a random GUID unless it states otherwise).</summary>
    /// <param name="previous">The identifier generated last, or <see langword="null"/> when none has been.</param>
    string NextId(string? previous);
    /// <summary>Gets the lowest priority anything is sent with, <c>0</c>: the first configured level, and the only one when none are configured. Used for traffic that should yield to everything else, such as heartbeats.</summary>
    int LowestPriority { get; }
    /// <summary>Gets the highest priority anything is sent with: that of the last configured level, or <c>0</c> when none are configured. Used for traffic that must not wait behind anything else, such as the exchange that identifies a connection.</summary>
    int HighestPriority { get; }
    /// <summary>Returns <paramref name="priority"/> if it is one of the configured priority levels, or the lowest level otherwise (including when it is <see langword="null"/>), so nothing is ever sent with a priority the configuration does not define.</summary>
    /// <param name="priority">The level to resolve, a member of the enum stated for the priorities.</param>
    Enum ResolvePriority(Enum? priority);
    /// <summary>Returns <paramref name="priority"/> if it is one of the configured priority levels, the lowest level when it is <see langword="null"/>, and otherwise throws: a message is never created with a priority the configuration does not define.</summary>
    /// <param name="priority">The level, a member of the enum stated for the priorities, or <see langword="null"/> for the lowest.</param>
    /// <exception cref="ArgumentException"><paramref name="priority"/> is not a configured level.</exception>
    Enum RequirePriority(Enum? priority);
    /// <summary>Returns whether a message with these properties would be an alert, which the host's message handler decides from the message's fields: the engine makes the message and asks it.</summary>
    /// <param name="body">The body text, which a draft being composed does not have yet (an empty string).</param>
    /// <param name="priority">The priority level, a member of the enum stated for the priorities, or <see langword="null"/> for the lowest.</param>
    /// <param name="tag">The tag, or an empty string for none.</param>
    /// <param name="securityLevel">The security level name, or an empty string for none.</param>
    /// <param name="addresses">The recipients.</param>
    /// <exception cref="ArgumentException">The priority or security level is not a configured one.</exception>
    bool ComputeIsAlert(string body, Enum? priority, string tag, string securityLevel, IReadOnlyList<AddressRequest> addresses);
    /// <summary>Returns why the received message <paramref name="message"/> carries a priority or security level that is not a configured one, or <see langword="null"/> when it does not (or it is not a message).</summary>
    /// <param name="message">An instance of <see cref="FrameType"/>.</param>
    string? GetUnconfiguredLevelReason(object message);
    /// <summary>Checks what the engine can only check once its handlers exist: that every priority a handler names is a configured level. Called when the engine starts.</summary>
    /// <exception cref="InvalidOperationException">A handler names a priority that is not configured.</exception>
    void Validate();
    /// <summary>Returns the configured priority level whose enum member has the integer value <paramref name="value"/>, or the lowest level when there is none. This is how a level stored in a draft or export becomes a level again.</summary>
    /// <param name="value">The stored value.</param>
    Enum PriorityOf(int? value);
    /// <summary>Gets the integer value of <paramref name="priority"/> as it is stored in drafts and exports: the value of its enum member, or of the lowest level's member when it is not a configured one.</summary>
    /// <param name="priority">The level.</param>
    int StoredPriority(Enum priority);
    /// <summary>Gets the name of <paramref name="priority"/> as it is shown to users: the name of the level, or of the lowest level when it is not a configured one.</summary>
    /// <param name="priority">The level.</param>
    string NameOf(Enum priority);
    /// <summary>Gets the priority level of <paramref name="message"/>, one of the configured priorities: what its handler reads, brought within them by <see cref="ResolvePriority"/>.</summary>
    /// <param name="message">An instance of <see cref="FrameType"/> the message handler recognizes.</param>
    Enum GetMessagePriority(object message);
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
    /// <summary>Gets the security level <paramref name="frame"/> carries as the host's enum member, or <see langword="null"/> for none or when it is not a message.</summary>
    /// <param name="frame">An instance of <see cref="FrameType"/>.</param>
    Enum? GetSecurityLevelKey(object frame);
    /// <summary>Gets the name of the configured security level <paramref name="level"/>, or an empty string for <see langword="null"/>.</summary>
    /// <param name="level">The level, a member of the enum stated for the security levels, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException"><paramref name="level"/> is not a configured security level.</exception>
    string GetSecurityLevelName(Enum? level);

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

    /// <summary>Returns the name, as the network configuration file spells it, of the user <paramref name="name"/> names (compared case-insensitively), or <see langword="null"/> when the network has no such user.</summary>
    /// <param name="name">The user name to look for.</param>
    string? FindUserName(string name);
    /// <summary>
    /// Returns why <paramref name="userName"/> cannot run on this node as that user, or <see langword="null"/> when it can: the network's <c>CertificateStore</c> and <c>AuthorityCertificate</c> are set, the store holds the file <c>{userName}.pfx</c>,
    /// the certificate in it has the user name as its subject common name, and it is signed by the authority certificate, which is the only authority trusted.
    /// </summary>
    /// <param name="userName">The user name to check.</param>
    string? GetCertificateProblem(string userName);    /// <summary>
                                                       /// Returns what is known about <paramref name="userName"/>: what the network configuration file states (see <see cref="IEngineBuilder{TFrame, TPacket, TPriority, TLevel}.CommandLineOverrides"/>), or a user with just
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
    /// <see cref="IEntryBarViewModel.DeleteEntry"/> before deleting; when <see langword="false"/>,
    /// the delete is silently skipped.
    /// </summary>
    /// <param name="folderType">The root folder type the entry or subfolder being deleted belongs to.</param>
    bool CanDelete(FolderType folderType);
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
    private readonly IMicroGatePortSource portSource = services?.GetService<IMicroGatePortSource>() ?? new MicroGatePortSource();

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
    private readonly Lazy<IHeartbeatFrameHandler?> heartbeatHandler = new(() => builder.FrameMap!.Heartbeat?.Create(services));
    private readonly Lazy<IHeartbeatFrameHandler?> packetHeartbeatHandler = new(() => builder.PacketMap?.Heartbeat?.Create(services));
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
    private readonly Lazy<IDraftFrameHandler?> draftHandler = new(() => builder.DraftHandler?.Create(services));
    private readonly List<object> alertHistory = [];
    private readonly Lazy<IAlarmHandler?> alarmHandler = new(() => builder.AlarmHandler?.Create(services));
    private readonly Lazy<IPrintFrameHandler?> printHandler = new(() => builder.PrintHandler?.Create(services));
    private readonly Lazy<IDeleteHandler?> deleteHandler = new(() => builder.DeleteHandler?.Create(services));
    private readonly Lazy<INetworkHandler?> networkHandler = new(() => builder.NetworkHandler?.Create(services));
    private readonly IReadOnlyList<MessagePriorityOption> defaultPriorities = [new MessagePriorityOption { Name = "NORMAL", Value = 0, Key = NoPriority.Normal }];
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
    public virtual int PacketSize => builder.PacketSizeValue ?? 16 * 1024;
    /// <inheritdoc />
    public virtual int PacketWindow => builder.PacketWindowValue ?? 1;

    /// <summary>Gets the name of the folder under the application data root that holds the app's data: what the display handler states, otherwise the entry assembly's name. It is not the app name, so renaming the app never moves the data.</summary>
    protected string DataFolderName => builder.DisplayHandlerInstance?.DataFolderName.OrNull() ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "App";

    /// <inheritdoc />
    public virtual string AppName => builder.DisplayHandlerInstance?.AppName.OrNull() ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <inheritdoc />
    public virtual string AppVersion => builder.DisplayHandlerInstance?.Version.OrNull() ?? (Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0");
    /// <inheritdoc />
    public virtual string AppDataRoot => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    /// <inheritdoc />
    public virtual string AppDataPath => currentUserProvider.UserName is { Length: > 0 } user ? Path.Combine(AppDataRoot, DataFolderName, user) : Path.Combine(AppDataRoot, DataFolderName);
    /// <inheritdoc />
    public virtual string UserFilePath => Path.Combine(AppDataRoot, DataFolderName, "User.json");
    /// <inheritdoc />
    public virtual bool IsKioskMode => builder.DisplayHandlerInstance?.IsKiosk ?? false;
    /// <inheritdoc />
    public virtual string GetNetworkIndicatorLabel(bool isOnline)
        => (isOnline ? builder.DisplayHandlerInstance?.NetworkOnlineLabel : builder.DisplayHandlerInstance?.NetworkOfflineLabel).OrNull() ?? (isOnline ? "ONLINE" : "OFFLINE");
    /// <inheritdoc />
    public virtual string GetNetworkIndicatorColor(bool isOnline)
        => (isOnline ? builder.DisplayHandlerInstance?.NetworkOnlineColor : builder.DisplayHandlerInstance?.NetworkOfflineColor).OrNull() ?? (isOnline ? "#2E7D32" : "#D35400");
    /// <inheritdoc />
    public virtual bool SeparateAlerts => builder.DisplayHandlerInstance?.SeparateAlerts ?? false;
    /// <inheritdoc />
    public virtual string HomeText => builder.DisplayHandlerInstance?.HomeText.OrNull() ?? "HOME";
    private readonly NetworkConfig network = networkConfig ?? new();

    private UserInfo? CurrentUserInfo => currentUserProvider.UserName is { Length: > 0 } name ? GetUserInfo(name) : null;

    /// <inheritdoc />
    public virtual string? WindowIconPath => builder.DisplayHandlerInstance?.Icon.OrNull();

    /// <inheritdoc />
    public virtual string? DebugUserName => null;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> Users
    {
        get
        {
            List<string> names = [];
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
            Dictionary<string, IReadOnlyList<string>> groups = new(StringComparer.OrdinalIgnoreCase);
            foreach ((string name, List<string> members) in network.UserGroups) { groups[name] = members; }
            return groups;
        }
    }

    /// <inheritdoc />
    public virtual int PeerPort => CurrentUserInfo?.MsmtPort ?? 50021;
    /// <inheritdoc />
    public virtual int InterfacePort => CurrentUserInfo?.InterfacePort ?? 50020;

    /// <inheritdoc />
    public virtual string AlertLabel => builder.DisplayHandlerInstance?.AlertLabel.OrNull() ?? "ALERT";
    /// <inheritdoc />
    public virtual TimeSpan DisconnectAlarmDuration => alarmHandler.Value?.DisconnectDuration ?? TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual TimeSpan AlarmSoundDuration => alarmHandler.Value?.AlertDuration ?? TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual IReadOnlyList<string> AlertQuickReadKeys => messageHandler.Value.AlertQuickReadKeys;
    /// <inheritdoc />
    public virtual bool AcceptAlert(object message)
    {
        if (!GetIsAlert(message)) { return true; }

        lock (alertHistory)
        {
            bool kept = messageHandler.Value.FilterAlerts([.. alertHistory], message);
            if (!kept) { return false; }

            int limit = Math.Max(0, messageHandler.Value.AlertHistoryLimit);
            alertHistory.Add(message);
            while (alertHistory.Count > limit) { alertHistory.RemoveAt(0); }
            return true;
        }
    }
    /// <inheritdoc />
    public virtual DraftDefaults DraftDefaults
        => draftHandler.Value is { } handler ? new DraftDefaults(DraftTagRules.Filter(handler.DefaultTag ?? string.Empty), handler.DefaultPriority, handler.DefaultSecurityLevel) : DraftDefaults.None;

    /// <inheritdoc />
    public virtual TagRules DraftTagRules => draftHandler.Value?.TagRules ?? TagRules.Unrestricted;

    /// <inheritdoc />
    public virtual LineWidthRange? DraftLineWidth
        => draftHandler.Value is { } handler && (handler.DefaultLineWidth is not null || handler.MaxLineWidth is not null)
            ? new LineWidthRange(handler.DefaultLineWidth, Math.Max(1, handler.MinLineWidth), handler.MaxLineWidth)
            : null;

    /// <inheritdoc />
    public virtual string? GetDraftHeader(DraftContent draft) => draftHandler.Value?.GetHeader(draft).OrNull();

    /// <inheritdoc />
    public virtual IReadOnlyList<MessagePriorityOption> Priorities => builder.PriorityOptions.Count > 0 ? builder.PriorityOptions : defaultPriorities;
    /// <inheritdoc />
    public virtual bool TagsEnabled => draftHandler.Value?.EnableTags ?? true;
    /// <inheritdoc />
    public virtual string PriorityLabel => builder.DisplayHandlerInstance?.PriorityLabel.OrNull() ?? "Priority";
    /// <inheritdoc />
    public virtual string SecurityLevelLabel => builder.DisplayHandlerInstance?.SecurityLevelLabel.OrNull() ?? "Security Level";
    /// <inheritdoc />
    public virtual string Rename(string label)
        => (builder.DisplayHandlerInstance is { } display ? label switch
        {
            "Inbox" => display.InboxLabel,
            "Outbox" => display.OutboxLabel,
            "Drafts" => display.DraftsLabel,
            "Draft" => display.DraftLabel,
            "Notes" => display.NotesLabel,
            "Note" => display.NoteLabel,
            "Activity" => display.ActivityLabel,
            "Messages" => display.MessagesLabel,
            "Message" => display.MessageLabel,
            _ => null
        } : null).OrNull() ?? label;
    /// <inheritdoc />
    public virtual string AlertPluralLabel => PluralOf(builder.DisplayHandlerInstance?.AlertPluralLabel, builder.DisplayHandlerInstance?.AlertLabel, "ALERTS");
    /// <inheritdoc />
    public virtual string TagPluralLabel => PluralOf(builder.DisplayHandlerInstance?.TagPluralLabel, builder.DisplayHandlerInstance?.TagLabel, "Tags");
    /// <inheritdoc />
    public virtual string PriorityPluralLabel => PluralOf(builder.DisplayHandlerInstance?.PriorityPluralLabel, builder.DisplayHandlerInstance?.PriorityLabel, "Priorities");
    /// <inheritdoc />
    public virtual string SecurityLevelPluralLabel => PluralOf(builder.DisplayHandlerInstance?.SecurityLevelPluralLabel, builder.DisplayHandlerInstance?.SecurityLevelLabel, "Security Levels");
    /// <inheritdoc />
    public virtual string TagLabel => builder.DisplayHandlerInstance?.TagLabel.OrNull() ?? "Tag";
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
    public virtual bool PrintReceivedDefaultEnabled => printHandler.Value?.PrintReceivedByDefault ?? false;

    /// <inheritdoc />
    public virtual MsmtSessionPeerOptions ConnectionOptions => throw new InvalidOperationException("Peer authentication requires the network's CertificateStore and AuthorityCertificate to be set.");

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
    public virtual HdlcPeerOptions HdlcOptions => builder.HdlcOptionsValue ?? new();

    /// <inheritdoc />
    public virtual UserRole Role => CurrentUserInfo?.Role ?? UserRole.Client;
    /// <inheritdoc />
    public virtual IReadOnlyList<ConnectionPoint> OutgoingPoints
    {
        get
        {
            if (CurrentUserInfo is not { } current) { return []; }

            List<(UserLink Link, bool IsParent)> links = [.. current.Parent is { } parent ? [(parent, true)] : Array.Empty<(UserLink, bool)>(), .. current.Children.Select(child => (child, false))];
            List<ConnectionPoint> points = [.. LinkPoints(current, links)];
            return [.. points.DistinctBy(point => point.Key)];
        }
    }
    /// <inheritdoc />
    public virtual string? ParentUser => CurrentUserInfo?.Parent?.User;
    /// <inheritdoc />
    public virtual IReadOnlyList<ConnectionPoint> ParentPoints => CurrentUserInfo is { Parent: { } parent } current ? LinkPoints(current, [(parent, true)]) : [];

    private static ConnectionMode ModeOf(UserLink link, bool isParent) => link.Mode ?? (isParent ? ConnectionMode.MsmtConnect : ConnectionMode.MsmtListen);

    // The points a node dials for some of its links: for each MsmtConnect link the other user's IP host and MSMT port, and, for all its Hdlc links together, one point
    // per HDLC port the node opens, which tries each of those links' users in turn since nothing says which port is cabled to whom.
    private List<ConnectionPoint> LinkPoints(UserInfo current, IEnumerable<(UserLink Link, bool IsParent)> links)
    {
        List<ConnectionPoint> points = [];
        List<HdlcRemote> remotes = [];
        foreach ((UserLink link, bool isParent) in links)
        {
            switch (ModeOf(link, isParent))
            {
                case ConnectionMode.MsmtConnect:
                    UserInfo target = GetUserInfo(link.User);
                    points.Add(new ConnectionPoint { IpAddress = target.IpHost ?? "127.0.0.1", Port = target.MsmtPort ?? 50021 });
                    break;
                case ConnectionMode.Hdlc:
                    remotes.Add(new HdlcRemote(link.User, GetUserInfo(link.User).HdlcAddress ?? 1));
                    break;
            }
        }

        if (remotes.Count > 0)
        {
            byte local = current.HdlcAddress ?? 1;
            if (remotes.FirstOrDefault(remote => remote.Address == local) is { } same)
            {
                throw new InvalidOperationException($"{current.Name} and {same.User} are linked by HDLC but both use the station address {local}; the local and remote addresses must differ");
            }

            if (remotes.GroupBy(remote => remote.Address).FirstOrDefault(group => group.Count() > 1) is { } shared)
            {
                throw new InvalidOperationException($"{string.Join(" and ", shared.Select(remote => remote.User))} are linked to {current.Name} by HDLC but both use the station address {shared.Key}; each user needs its own");
            }

            points.AddRange(HdlcPortNames(current).Select(port => new ConnectionPoint
            {
                SerialPort = port,
                SerialAddress = current.HdlcAddress ?? 1,
                RemoteSerialAddress = remotes[0].Address,
                User = remotes[0].User,
                OtherRemotes = [.. remotes.Skip(1)]
            }));
        }

        return points;
    }

    private IReadOnlyList<string> HdlcPortNames(UserInfo current)
    {
        if (current.HdlcPorts is not ["*"]) { return current.HdlcPorts; }

        // IEngineController.OutgoingPoints is a plain property that the peer services read from their synchronous reconfigure, so the one asynchronous port lookup is run to completion here.
        return Task.Run(async () => await portSource.GetPorts(CancellationToken.None)).GetAwaiter().GetResult();
    }

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

    private int PriorityValue(Enum key)
        => builder.PriorityOptions.Count == 0 ? 0
        : Priorities.FirstOrDefault(priority => key.Equals(priority.Key))?.Value
            ?? throw new InvalidOperationException($"A priority of {key.GetType().Name}.{key} is used, which is not one of the configured priorities: {string.Join(", ", Priorities.Select(priority => priority.Name))}");

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
    public virtual INetworkHandler? NetworkHandler => networkHandler.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<ExportFormatDefinition> ExportFormats => exportFormats.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<ImportFormatDefinition> ImportFormats => importFormats.Value;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> StorageServers => [.. Servers.Keys];
    /// <inheritdoc />
    public virtual IReadOnlyList<AutoForwardControllerDefinition> AutoForwardControllers => autoForwardControllers.Value;


    /// <inheritdoc />
    public virtual object CreateFrame() => frame.Create();
    /// <inheritdoc />
    public virtual string GetMessageId(object value) => messageHandler.Value.GetId(value);
    /// <inheritdoc />
    public virtual void SetMessageId(object value, string id) => messageHandler.Value.SetId(value, id);
    /// <inheritdoc />
    public virtual string GetFromUser(object value)
        => IsMessage(value) ? messageHandler.Value.GetSender(value)
        : IsRetrieval(value) ? retrievalHandler.Value.GetSender(value)
        : IsReadReceipt(value) ? readReceiptHandler.Value.GetSender(value)
        : IsReceiveReceipt(value) ? receiveReceiptHandler.Value.GetSender(value)
        : string.Empty;
    /// <inheritdoc />
    public virtual void SetFromUser(object value, string user)
    {
        if (IsMessage(value)) { messageHandler.Value.SetSender(value, user); }
        else if (IsRetrieval(value)) { retrievalHandler.Value.SetSender(value, user); }
        else if (IsReadReceipt(value)) { readReceiptHandler.Value.SetSender(value, user); }
        else if (IsReceiveReceipt(value)) { receiveReceiptHandler.Value.SetSender(value, user); }
    }
    /// <inheritdoc />
    public virtual List<MessageAddress> GetAddresses(object value) => messageHandler.Value.GetAddresses(value);
    /// <inheritdoc />
    public virtual void SetAddresses(object value, List<MessageAddress> addresses) => messageHandler.Value.SetAddresses(value, addresses);
    /// <inheritdoc />
    public virtual IReadOnlyList<string> Route(object value)
        => IsMessage(value) ? [.. GetAddresses(value).Where(address => address.Type != AddressType.External).Select(address => address.UserName).Distinct(StringComparer.OrdinalIgnoreCase)]
        : IsRetrieval(value) ? NonEmpty(retrievalHandler.Value.GetDestination(value))
        : IsReadReceipt(value) ? NonEmpty(readReceiptHandler.Value.GetDestination(value))
        : IsReceiveReceipt(value) ? NonEmpty(receiveReceiptHandler.Value.GetDestination(value))
        : [];

    private string PluralOf(string? plural, string? singular, string fallback)
        => plural.OrNull() ?? (singular.OrNull() is { } stated ? (stated.EndsWith('s') ? stated : stated + "s") : fallback);

    private string[] NonEmpty(string destination) => string.IsNullOrWhiteSpace(destination) ? [] : [destination];
    /// <inheritdoc />
    public virtual DateTime GetSentAt(object value) => messageHandler.Value.GetSentAt(value);
    /// <inheritdoc />
    public virtual bool IsMessage(object value) => messageHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateMessage(MessageContent context) => messageHandler.Value.Create(context with { Priority = RequirePriority(context.Priority) });
    /// <inheritdoc />
    public virtual string GetBody(object value) => messageHandler.Value.GetBody(value);
    /// <inheritdoc />
    public virtual bool GetIsAlert(object value) => messageHandler.Value.IsAlert(value);
    /// <inheritdoc />
    public virtual string NextId(string? previous) => messageHandler.Value.NextId(previous);
    /// <inheritdoc />
    public virtual int LowestPriority => 0;
    /// <inheritdoc />
    public virtual int HighestPriority => Priorities.Max(priority => priority.Value);
    /// <inheritdoc />
    public virtual Enum ResolvePriority(Enum? priority) => (Priorities.FirstOrDefault(level => priority is not null && priority.Equals(level.Key)) ?? Priorities[0]).Key;
    /// <inheritdoc />
    public virtual bool ComputeIsAlert(string body, Enum? priority, string tag, string securityLevel, IReadOnlyList<AddressRequest> addresses)
    {
        object message = CreateMessage(new MessageContent { SentAt = DateTime.UtcNow, Body = body, Priority = RequirePriority(priority), Tag = tag, SecurityLevel = securityLevel });
        SetAddresses(message, [.. addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })]);
        return messageHandler.Value.IsAlert(message);
    }

    /// <inheritdoc />
    public virtual Enum RequirePriority(Enum? priority)
        => priority is null ? Priorities[0].Key
        : Priorities.FirstOrDefault(level => priority.Equals(level.Key))?.Key
            ?? throw new ArgumentException($"A priority of {priority.GetType().Name}.{priority} is used, which is not one of the configured priorities: {string.Join(", ", Priorities.Select(level => level.Name))}", nameof(priority));

    /// <inheritdoc />
    public virtual string? GetUnconfiguredLevelReason(object message)
    {
        if (!IsMessage(message)) { return null; }

        Enum priority = messageHandler.Value.GetPriority(message);
        if (!Priorities.Any(level => priority.Equals(level.Key))) { return $"has the priority {priority.GetType().Name}.{priority}, which is not a configured priority"; }

        return messageHandler.Value.GetSecurityLevelKey(message) is { } security && !SecurityLevels.Any(level => security.Equals(level.Key))
            ? $"has the security level {security.GetType().Name}.{security}, which is not a configured security level"
            : null;
    }

    /// <inheritdoc />
    public virtual void Validate()
    {
        if (HeartbeatsEnabled) { PriorityValue(heartbeatHandler.Value!.Priority); }

        PriorityValue(retrievalHandler.Value.Priority);
        PriorityValue(readReceiptHandler.Value.Priority);
        PriorityValue(receiveReceiptHandler.Value.Priority);
        if (packetHeartbeatHandler.Value is { } packetHeartbeat) { PriorityValue(packetHeartbeat.Priority); }
    }

    /// <inheritdoc />
    public virtual Enum PriorityOf(int? value) => (Priorities.FirstOrDefault(level => level.Stored == value) ?? Priorities[0]).Key;
    /// <inheritdoc />
    public virtual int StoredPriority(Enum priority) => (Priorities.FirstOrDefault(level => priority.Equals(level.Key)) ?? Priorities[0]).Stored;
    /// <inheritdoc />
    public virtual string NameOf(Enum priority) => (Priorities.FirstOrDefault(level => priority.Equals(level.Key)) ?? Priorities[0]).Name;
    /// <inheritdoc />
    public virtual Enum GetMessagePriority(object message) => ResolvePriority(messageHandler.Value.GetPriority(message));
    /// <inheritdoc />
    public virtual int GetPriority(object value)
        => IsMessage(value) ? Priorities.First(level => level.Key.Equals(GetMessagePriority(value))).Value
        : IsHeartbeat(value) ? PriorityValue(heartbeatHandler.Value!.Priority)
        : IsRetrieval(value) ? PriorityValue(retrievalHandler.Value.Priority)
        : IsReadReceipt(value) ? PriorityValue(readReceiptHandler.Value.Priority)
        : IsReceiveReceipt(value) ? PriorityValue(receiveReceiptHandler.Value.Priority)
        : 0;
    /// <inheritdoc />
    public virtual string GetTag(object value) => messageHandler.Value.GetTag(value);
    /// <inheritdoc />
    public virtual Enum? GetSecurityLevelKey(object value) => IsMessage(value) ? messageHandler.Value.GetSecurityLevelKey(value) : null;

    /// <inheritdoc />
    public virtual string GetSecurityLevelName(Enum? level)
        => level is null ? string.Empty
        : SecurityLevels.FirstOrDefault(candidate => level.Equals(candidate.Key))?.Name
            ?? throw new ArgumentException($"A security level of {level.GetType().Name}.{level} is used, which is not one of the configured security levels: {string.Join(", ", SecurityLevels.Select(candidate => candidate.Name))}", nameof(level));

    /// <inheritdoc />
    public virtual string GetSecurityLevel(object value) => IsMessage(value) ? messageHandler.Value.GetSecurityLevel(value) : string.Empty;
    /// <inheritdoc />
    public virtual bool IsReadReceipt(object value) => readReceiptHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateReadReceipt(string messageId, string to) => readReceiptHandler.Value.Create(new ReceiptCreateContext { MessageId = messageId, To = to });
    /// <inheritdoc />
    public virtual string GetReadReceiptMessageId(object value) => readReceiptHandler.Value.GetMessageId(value);
    /// <inheritdoc />
    public virtual bool IsReceiveReceipt(object value) => receiveReceiptHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual bool HeartbeatsEnabled => frame.Heartbeat is not null;
    /// <inheritdoc />
    public virtual bool PacketHeartbeatsEnabled => packet?.Heartbeat is not null;
    /// <inheritdoc />
    public virtual object CreatePacketHeartbeat() => (packetHeartbeatHandler.Value ?? throw new InvalidOperationException("No heartbeat packet handler is stated; state one with Heartbeat<THandler>() on the packet configuration.")).Create();
    /// <inheritdoc />
    public virtual bool IsPacketHeartbeat(object packet) => packetHeartbeatHandler.Value?.IsValid(packet) ?? false;
    /// <inheritdoc />
    public virtual TimeSpan HeartbeatInterval => (packetHeartbeatHandler.Value ?? heartbeatHandler.Value)?.Interval ?? TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual TimeSpan HeartbeatRetryInterval => (packetHeartbeatHandler.Value ?? heartbeatHandler.Value)?.RetryInterval ?? TimeSpan.FromSeconds(2);
    /// <inheritdoc />
    public virtual int PacketHeartbeatPriority => PriorityValue((packetHeartbeatHandler.Value ?? throw new InvalidOperationException("No heartbeat packet handler is stated.")).Priority);
    /// <inheritdoc />
    public virtual object CreateHeartbeat() => (heartbeatHandler.Value ?? throw new InvalidOperationException("No heartbeat handler is stated; state one with Heartbeat<THandler>() on the frame configuration.")).Create();
    /// <inheritdoc />
    public virtual bool IsHeartbeat(object value) => heartbeatHandler.Value?.IsValid(value) ?? false;
    /// <inheritdoc />
    public virtual object CreateReceiveReceipt(string messageId, string to) => receiveReceiptHandler.Value.Create(new ReceiptCreateContext { MessageId = messageId, To = to });
    /// <inheritdoc />
    public virtual string GetReceiveReceiptMessageId(object value) => receiveReceiptHandler.Value.GetMessageId(value);
    /// <inheritdoc />
    public virtual bool IsRetrieval(object value) => retrievalHandler.Value.IsValid(value);
    /// <inheritdoc />
    public virtual object CreateRetrieval(RetrievalCriteria criteria, string server) => retrievalHandler.Value.Create(new RetrievalCreateContext
    {
        Server = server,
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
    public virtual string? FindUserName(string name) => network.Users.Keys.FirstOrDefault(user => string.Equals(user, name, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public virtual string? GetCertificateProblem(string userName)
        => MsmtCertificateLookup.GetProblem(network.GetCertificatePath(userName), network.GetAuthorityCertificatePath(), userName);
    /// <inheritdoc />
    public virtual UserInfo GetUserInfo(string userName)
    {
        UserInfo info = network.GetUserInfo(userName) ?? new UserInfo { Name = userName };
        return info with { Groups = [.. UserGroups.Where(group => group.Value.Contains(userName, StringComparer.OrdinalIgnoreCase)).Select(group => group.Key)] };
    }

    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, string> GetUserData(string userName) => GetUserInfo(userName).Data;

    /// <inheritdoc />
    public virtual IConnectionInfo WithLocalUser(IConnectionInfo connection) => connection;

    /// <inheritdoc />
    public virtual int GetPrintCount(object value) => printHandler.Value?.GetPrintCount(value) ?? 1;

    /// <inheritdoc />
    public virtual bool CanDelete(FolderType folderType) => deleteHandler.Value?.CanDelete(new DeleteContext { Folder = folderType }) ?? true;


    private PacketMap Packet => packet ?? throw new NotSupportedException("This engine has no packet type; state one with Packets<TPacket>(...) to enable packetization.");
}

/// <summary>Extension members for <see cref="IEngineController"/>.</summary>
internal static class EngineControllerExtensions
{
    private static readonly Regex conceptPattern = new(@"\b(Security Levels|Security Level|Priorities|Priority|Alerts|Alert|Tags|Tag|Inbox|Outbox|Drafts|Draft|Notes|Note|Activity|Messages|Message)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    extension(IEngineController engineController)
    {
        /// <summary>Returns why <paramref name="frameValue"/> is an invalid message, which is dropped with an error logged, or <see langword="null"/> when it is valid or not a message. A message always has an identifier before it is sent, and only carries priorities and security levels the configuration defines.</summary>
        /// <param name="frameValue">An instance of <see cref="IEngineController.FrameType"/>.</param>
        public string? GetInvalidMessageReason(object frameValue)
            => !engineController.IsMessage(frameValue) ? null
            : string.IsNullOrEmpty(engineController.GetMessageId(frameValue)) ? "has no identifier"
            : engineController.GetUnconfiguredLevelReason(frameValue);

        /// <summary>Returns <paramref name="text"/>, a fixed piece of the user interface written with the engine's own names for concepts, with each concept name replaced by what the host calls it: the root folders, drafts, notes, messages and activity through the display handler's members for them, and alerts, tags, priorities and security levels through their own labels. A replacement keeps the case style of what it replaces (all capitals, all lowercase, or as written). Text with none of them, or with no host names for them, comes back as it is.</summary>
        /// <param name="text">The text, written with the engine's own names.</param>
        public string Display(string text)
        {
            if (!conceptPattern.IsMatch(text)) { return text; }

            Dictionary<string, string?> replacements = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Security Levels"] = engineController.SecurityLevelPluralLabel,
                ["Security Level"] = engineController.SecurityLevelLabel,
                ["Priorities"] = engineController.PriorityPluralLabel,
                ["Priority"] = engineController.PriorityLabel,
                ["Alerts"] = engineController.AlertPluralLabel,
                ["Alert"] = engineController.AlertLabel,
                ["Tags"] = engineController.TagPluralLabel,
                ["Tag"] = engineController.TagLabel
            };
            foreach (string concept in new[] { "Inbox", "Outbox", "Drafts", "Draft", "Notes", "Note", "Activity", "Messages", "Message" }) { replacements[concept] = engineController.Rename(concept); }

            return conceptPattern.Replace(text, match =>
            {
                string replacement = replacements[match.Value] is { Length: > 0 } named ? named : match.Value;
                if (string.Equals(replacement, match.Value, StringComparison.OrdinalIgnoreCase)) { return match.Value; }

                return match.Value.Length > 1 && match.Value == match.Value.ToUpperInvariant() ? replacement.ToUpperInvariant()
                    : match.Value == match.Value.ToLowerInvariant() ? replacement.ToLowerInvariant()
                    : replacement;
            });
        }

        /// <summary>Gets the identifier a frame is known by: a message's own, the message a receipt is for, or an empty string for any other frame, which has none.</summary>
        /// <param name="frameValue">An instance of <see cref="IEngineController.FrameType"/>.</param>
        public string GetIdentifier(object frameValue)
            => engineController.IsMessage(frameValue) ? engineController.GetMessageId(frameValue)
            : engineController.IsReadReceipt(frameValue) ? engineController.GetReadReceiptMessageId(frameValue)
            : engineController.IsReceiveReceipt(frameValue) ? engineController.GetReceiveReceiptMessageId(frameValue)
            : string.Empty;

        /// <summary>
        /// Reads every logical field of <paramref name="payload"/> (an instance of <see cref="IEngineController.FrameType"/>)
        /// into a new <see cref="MessageReceivedEvent"/>. Shared by <see cref="DirectServiceConnection"/> and
        /// <see cref="EngineHooksService"/>, so both surface the exact same fields for an inbound message.
        /// </summary>
        public MessageReceivedEvent ToMessageReceivedEvent(object payload) => new()
        {
            MessageId = engineController.GetMessageId(payload),
            FromUser = engineController.GetFromUser(payload),
            Body = engineController.GetBody(payload),
            Addresses = [.. engineController.GetAddresses(payload).Select(a => new AddressRequest { UserName = a.UserName, Type = a.Type.ToString(), Information = a.Information })],
            SentAt = engineController.GetSentAt(payload),
            IsAlert = engineController.GetIsAlert(payload),
            Priority = engineController.GetMessagePriority(payload),
            Tag = engineController.GetTag(payload),
            SecurityLevel = engineController.GetSecurityLevelKey(payload)
        };
    }
}
