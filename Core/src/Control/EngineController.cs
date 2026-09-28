namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Single control interface consolidating every extension point through which a host application
/// customises Engine behaviour without modifying Engine code: the concrete message type and its logical
/// field mapping, how that message type is serialized and packetized (and at what packet size and window) for the network, app
/// identity/presentation, local user identity, the user/group directory, listener ports, alert settings,
/// message composition, the automatic print policy, MSMT peer certificate naming and peer options, network
/// topology, the points this node connects out to, how the user on the other end of a connection is identified
/// (optionally after a connection message exchange), the external systems this instance communicates with, and whether <c>config.json</c> is read at all. External drive discovery and printer discovery/driving are real
/// OS-level behavior, not configuration or rules, so they live on <see cref="Devices.IExternalDriveProvider"/>
/// and <see cref="Devices.IPrintDriver"/> instead. See <c>Docs/Components/Control.md</c>.
/// </summary>
public interface IEngineController
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
    /// <summary>When <see langword="true"/>, a <c>--config</c> argument is read; when <see langword="false"/> (the default), it is ignored and <see cref="EngineConfig"/> always uses its defaults.</summary>
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
/// Shared certificate store lookup for <see cref="DefaultEngineController{TMessage}.ConnectionOptions"/> and
/// <see cref="ConfiguredEngineController.ConnectionOptions"/> — not itself generic over the message type, since
/// certificate lookup has nothing to do with it.
/// </summary>
internal static class MsmtCertificateLookup
{
    /// <summary>
    /// Looks up <paramref name="currentUserName"/>'s identity certificate via <paramref
    /// name="getCertificateName"/> and <paramref name="trustedAuthorityCertificateName"/>'s certificate
    /// authority (each caller passes its own, potentially config-overridden, values) in the system
    /// certificate store.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="currentUserName"/> is <see langword="null"/>, so no identity certificate can be resolved.</exception>
    public static MsmtSessionPeerOptions BuildPeerOptions(string? currentUserName, Func<string, string> getCertificateName, string trustedAuthorityCertificateName)
    {
        if (currentUserName is null)
        {
            throw new InvalidOperationException("Peer connection options require a registered current user to resolve an identity certificate for.");
        }

        string certName = getCertificateName(currentUserName);
        X509Certificate2 identity = FindCertificate(certName)
            ?? throw new InvalidOperationException($"Peer authentication requires a certificate named '{certName}', but none was found in the system store. Install the certificate to continue.");
        X509Certificate2 authority = FindCertificate(trustedAuthorityCertificateName)
            ?? throw new InvalidOperationException($"Peer authentication requires a trusted authority certificate named '{trustedAuthorityCertificateName}', but none was found in the system store. Install the certificate to continue.");

        return new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = [authority] },
            RequireFullyQualifiedHostname = false
        };
    }

    /// <summary>
    /// Builds peer options directly from certificate files on disk instead of a system store lookup - used
    /// when <c>config.json</c>'s <c>PeerCertificateFile</c>/<c>TrustedAuthorityCertificateFile</c> fields
    /// are set. <paramref name="peerCertificateFile"/> must be a PKCS#12 file carrying the identity
    /// certificate's private key; <paramref name="trustedAuthorityCertificateFile"/> a public certificate
    /// file for the trusted authority.
    /// </summary>
    /// <exception cref="InvalidOperationException">Either file does not exist.</exception>
    public static MsmtSessionPeerOptions BuildPeerOptionsFromFiles(string peerCertificateFile, string trustedAuthorityCertificateFile)
    {
        if (!File.Exists(peerCertificateFile))
        {
            throw new InvalidOperationException($"Peer authentication requires an identity certificate file at '{peerCertificateFile}', but it was not found.");
        }
        if (!File.Exists(trustedAuthorityCertificateFile))
        {
            throw new InvalidOperationException($"Peer authentication requires a trusted authority certificate file at '{trustedAuthorityCertificateFile}', but it was not found.");
        }

        X509Certificate2 identity = X509CertificateLoader.LoadPkcs12FromFile(peerCertificateFile, password: null);
        X509Certificate2 authority = X509CertificateLoader.LoadCertificateFromFile(trustedAuthorityCertificateFile);

        return new MsmtSessionPeerOptions
        {
            Credentials = new MsmtCredentials { Identity = identity, TrustedAuthorities = [authority] },
            RequireFullyQualifiedHostname = false
        };
    }

    private static X509Certificate2? FindCertificate(string name)
    {
        foreach (StoreLocation location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
        {
            using X509Store store = new(StoreName.My, location);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (string.Equals(cert.GetNameInfo(X509NameType.SimpleName, false), name, StringComparison.OrdinalIgnoreCase))
                    {
                        return cert;
                    }
                }
            }
            catch { }
        }
        return null;
    }
}

/// <summary>
/// Engine-level decorator applying every <c>config.json</c> field over whichever <see cref="IEngineController"/>
/// is registered (a host's <see cref="DefaultEngineController{TMessage}"/> subclass) — field by field, for
/// just the members that have a corresponding <c>config.json</c> field; every other member, including the
/// entire message-format surface, delegates straight to the wrapped provider. Registered by
/// <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
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
    public ConfiguredEngineController(IEngineController fallback, EngineConfig config, ICurrentUserProvider currentUserProvider)
    {
        this.fallback = fallback;
        this.config = config;
        this.currentUserProvider = currentUserProvider;
        userData = config.GetUserData();
    }

    private readonly IEngineController fallback;
    private readonly EngineConfig config;
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
