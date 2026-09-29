namespace BlueHeighliner.Comlink;

/// <summary>
/// The fluent surface a host uses to say how the engine runs, handed to <see cref="IEngineConfiguration.Configure"/>.
/// Every call returns the builder, and every call is optional except <see cref="Message{TMessage}"/>: anything left
/// unstated takes the engine's default. Where a setting also exists in the <c>--config</c> file, the file's value wins
/// over what is stated here (see <c>Docs/Components/Config.md</c>). See <c>Docs/Components/Configuration.md</c> for what
/// each group of settings does.
/// </summary>
public interface IEngineBuilder
{
    /// <summary>
    /// States the host's message type and how the engine's logical message fields map onto it. Required. The type must be
    /// LiteDB-serializable for storage, and must satisfy whatever serializer is used for the network (by default
    /// protobuf-net, so it needs <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes).
    /// </summary>
    /// <typeparam name="TMessage">The host's message type.</typeparam>
    /// <param name="map">Maps each logical field.</param>
    IEngineBuilder Message<TMessage>(Action<IMessageBuilder<TMessage>> map) where TMessage : class, new();

    /// <summary>
    /// Turns on packetization: payloads are broken into prioritized packets of type <typeparamref name="TPacket"/> and
    /// reassembled on the other side, so a large payload does not hold up higher-priority ones. Off by default. Every
    /// node on a network must be configured alike, since neither side can tell whether the other packetizes.
    /// </summary>
    /// <typeparam name="TPacket">The host's packet type.</typeparam>
    /// <param name="map">Maps the packet fields and sets the packet size and window.</param>
    IEngineBuilder Packets<TPacket>(Action<IPacketBuilder<TPacket>> map) where TPacket : class, new();

    /// <summary>Sets the application name, used as the default data folder name and in log headers. Defaults to the entry assembly's name.</summary>
    IEngineBuilder AppName(string name);

    /// <summary>Sets the application version, shown in the title bar and the info popup. Defaults to the entry assembly's version.</summary>
    IEngineBuilder AppVersion(string version);

    /// <summary>Sets the absolute path of the application data directory. Defaults to a folder named after <see cref="AppName"/> in the user's application data.</summary>
    IEngineBuilder DataPath(string path);

    /// <summary>Turns kiosk mode on or off. Kiosk mode hides window chrome and restricts navigation.</summary>
    IEngineBuilder KioskMode(bool enabled = true);

    /// <summary>Sets the text shown in the content area when no entry is selected.</summary>
    IEngineBuilder HomeText(string text);

    /// <summary>Sets the <c>avares://</c> URI of the window icon. Defaults to the operating system's.</summary>
    IEngineBuilder WindowIcon(Uri uri);

    /// <summary>Sets a user name for development and testing, which skips the installed user lookup.</summary>
    IEngineBuilder DebugUser(string userName);

    /// <summary>Sets how an installation code entered by the user resolves to a user. Defaults to accepting the code <c>CODE</c> for a user named <c>TEST</c>.</summary>
    /// <param name="resolve">Returns the user for a code, or <see langword="null"/> when the code is not recognized.</param>
    IEngineBuilder UserCodes(Func<string, UserInfo?> resolve);

    /// <summary>Adds user names to the directory used for address auto-complete and for connection identification.</summary>
    IEngineBuilder Users(params string[] names);

    /// <summary>Defines a group of users, whose members may be user names or other group names.</summary>
    IEngineBuilder Group(string name, params string[] members);

    /// <summary>Attaches app-specific data to a user, merged with anything already attached to that user. The engine does not interpret it; it travels with the user's <see cref="UserIdentity"/>.</summary>
    IEngineBuilder UserData(string userName, IReadOnlyDictionary<string, string> data);

    /// <summary>Sets how the data attached to any user is looked up, in place of the per-user calls.</summary>
    IEngineBuilder UserData(Func<string, IReadOnlyDictionary<string, string>> lookup);

    /// <summary>
    /// Defines the ordered set of security levels a message may be sent at, from lowest to highest: each level
    /// ranks higher than the one stated before it. Each level is a display name paired with the hex color shown
    /// for it in the top banner. Empty (the default) turns the whole feature off: every message maps to an empty
    /// security level and no destination is ever blocked for lacking one.
    /// </summary>
    IEngineBuilder SecurityLevels(params (string Name, string Color)[] levels);

    /// <summary>
    /// Assigns a user to run at a security level by name (see <see cref="SecurityLevels"/>), merged with anything
    /// already assigned. A user with no assignment runs at the lowest configured level.
    /// </summary>
    IEngineBuilder UserSecurityLevel(string userName, string levelName);

    /// <summary>Sets how the security level for any user name is looked up, in place of the per-user calls.</summary>
    IEngineBuilder UserSecurityLevel(Func<string, string> lookup);

    /// <summary>Sets the TCP port this node listens on for IP connections from other nodes. Defaults to 50021.</summary>
    IEngineBuilder PeerPort(int port);

    /// <summary>Sets the loopback TCP port the local interface listener uses. Defaults to 50020.</summary>
    IEngineBuilder InterfacePort(int port);

    /// <summary>Sets the text shown in the title bar's alert box while alarming, and the draft editor's alert checkbox label. Defaults to <c>ALERT</c>.</summary>
    IEngineBuilder AlertLabel(string label);

    /// <summary>Sets how long the alarm sound plays after an alert is received. Defaults to 30 seconds.</summary>
    IEngineBuilder AlarmDuration(TimeSpan duration);

    /// <summary>Sets whether clicking the alert box, or pressing Space or Enter outside a text input, confirms the latest unconfirmed alert. On by default.</summary>
    IEngineBuilder QuickConfirmation(bool enabled = true);

    /// <summary>Sets whether the draft editor lets the user send a draft as an alert. On by default; turning it off never stops alerts from being received.</summary>
    IEngineBuilder ComposeAlerts(bool enabled = true);

    /// <summary>Sets the selectable priority levels, in display order, each a display name paired with its priority number. Defaults to a single level named <c>Normal</c> with the value 0.</summary>
    IEngineBuilder Priorities(params (string Name, int Value)[] priorities);

    /// <summary>Turns message tags on or off in the user interface, and optionally renames the tag input (for example to <c>Category</c>). On by default, labelled <c>Tag</c>.</summary>
    IEngineBuilder Tags(bool enabled = true, string? label = null);

    /// <summary>Blocks a tag and priority combination when composing a draft. Either may be <see langword="null"/> to match any value.</summary>
    IEngineBuilder BlockTag(string? tag, int? priority);

    /// <summary>
    /// Overrides the display label shown for an address type: in the address type picker, the per-address badge, and
    /// the message view's section headers. Defaults to the enum name (<c>To</c>, <c>Cc</c>, <c>External</c>).
    /// </summary>
    IEngineBuilder AddressTypeLabel(AddressType type, string label);

    /// <summary>Sets whether the print manager's "print received" toggle starts enabled, printing every received message from startup. Off by default.</summary>
    IEngineBuilder PrintReceived(bool enabledByDefault = true);

    /// <summary>Sets how many copies of a received message are printed while "print received" is on. Defaults to one for every message.</summary>
    /// <typeparam name="TMessage">The host's message type, as given to <see cref="Message{TMessage}"/>.</typeparam>
    IEngineBuilder PrintCount<TMessage>(Func<TMessage, int> copies) where TMessage : class;

    /// <summary>Sets which root folders let the user delete entries. Defaults to all of them.</summary>
    IEngineBuilder CanDelete(Func<FolderType, bool> allowed);

    /// <summary>Sets the certificate subject name that belongs to a user, for the local user the identity certificate to look up and for others the name their certificate is expected to carry. Defaults to the user name itself.</summary>
    IEngineBuilder CertificateName(Func<string, string> name);

    /// <summary>Sets the subject name of the certificate authority trusted to sign every peer's certificate. Defaults to <c>COMLINK-ROOT</c>.</summary>
    IEngineBuilder TrustedAuthority(string certificateName);

    /// <summary>Replaces how the MSMT peer options (identity certificate and trusted authorities) are built, for custom certificate pinning or a non-store certificate source.</summary>
    IEngineBuilder ConnectionOptions(Func<MsmtSessionPeerOptions> options);

    /// <summary>Sets the networking role. Defaults to <see cref="NodeRole.Peer"/>.</summary>
    IEngineBuilder Role(NodeRole role);

    /// <summary>Adds a point this node connects out to and keeps connected. A client connects to the first only.</summary>
    IEngineBuilder OutgoingPoint(ConnectionPoint point);

    /// <summary>Defines a server in the topology a <see cref="NodeRole.Server"/> routes with, and the child clients it owns.</summary>
    IEngineBuilder Server(string name, params string[] childClients);

    /// <summary>Sets who is on the other end of a connection that has just formed. Return <see langword="null"/> to leave it to the engine, which names an IP connection after the user whose certificate name it carries and a serial connection after its port.</summary>
    IEngineBuilder Identify(Func<ConnectionInfo, UserIdentity?> identify);

    /// <summary>
    /// Turns on the connection message: the node that opens a connection sends the message built by <paramref name="create"/>
    /// first (both nodes of a serial link do), and the connection is not usable until the exchange completes. Every node on a
    /// network must be configured alike.
    /// </summary>
    /// <typeparam name="TMessage">The type of the connection message.</typeparam>
    /// <param name="create">Builds the message for a connection, or returns <see langword="null"/> to send an empty one.</param>
    IEngineBuilder ConnectionMessage<TMessage>(Func<ConnectionInfo, TMessage?> create) where TMessage : class;

    /// <summary>
    /// Adds a reply to the connection message: the node that receives one answers with the response built by
    /// <paramref name="create"/>, which the opening node waits for. Only used together with <see cref="ConnectionMessage{TMessage}"/>.
    /// </summary>
    /// <typeparam name="TResponse">The type of the connection response.</typeparam>
    /// <param name="create">Builds the response, given the connection with the message just received, or returns <see langword="null"/> to send an empty one.</param>
    IEngineBuilder ConnectionResponse<TResponse>(Func<ConnectionInfo, TResponse?> create) where TResponse : class;

    /// <summary>Replaces the serializer for the connection message and response. The default builds only those two types with protobuf-net.</summary>
    IEngineBuilder ConnectionSerializer(INetworkSerializer serializer);

    /// <summary>Allows the <c>--config</c> command-line argument to be read. Off by default, in which case the argument is ignored.</summary>
    IEngineBuilder ConfigFile(bool enabled = true);

    /// <summary>Adds an external system, a conduit relaying messages to and from a system outside Comlink.</summary>
    IEngineBuilder ExternalSystem(IExternalSystem system);

    /// <summary>Designates an external system as the exclusive upstream hub every outgoing message is sent to instead of the peer network. It is added like <see cref="ExternalSystem"/> when it is not already.</summary>
    IEngineBuilder ExternalServer(IExternalSystem system);
}
