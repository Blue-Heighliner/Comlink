namespace BlueHeighliner.Comlink;

/// <summary>
/// The fluent surface a host uses to say how the engine runs, handed to <see cref="IEngineConfiguration.Configure"/>.
/// Every call returns the builder, and every call is optional except <see cref="Frames{TFrame}"/>: anything left
/// unstated takes the engine's default. Where a setting also exists in the <c>--config</c> file, the file's value wins
/// over what is stated here (see <c>Docs/Components/Config.md</c>). See <c>Docs/Components/Configuration.md</c> for what
/// each group of settings does.
/// </summary>
public interface IEngineBuilder
{
    /// <summary>
    /// States the host's frame type, the data format of all network traffic other than packets, and how the engine's logical fields map onto it, and what else depends on the frame type: the print
    /// count, auto forward controllers, the network processor and the initial frame processor. Required. The type must be LiteDB-serializable for
    /// storage, and must satisfy whatever serializer is used for the network (by default protobuf-net, so it needs
    /// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes).
    /// </summary>
    /// <typeparam name="TFrame">The host's frame type.</typeparam>
    /// <param name="map">Maps each logical field.</param>
    IEngineBuilder Frames<TFrame>(Action<IFrameBuilder<TFrame>> map) where TFrame : class, new();

    /// <summary>
    /// Turns on packetization: payloads are broken into prioritized packets of type <typeparamref name="TPacket"/> and
    /// reassembled on the other side, so a large payload does not hold up higher-priority ones, and states what else depends on the packet type, the
    /// initial packet processor. Off by default. Every node on a network must be configured alike, since neither side can tell whether the other packetizes.
    /// </summary>
    /// <typeparam name="TPacket">The host's packet type.</typeparam>
    /// <param name="map">Maps the packet fields and sets the packet size and window.</param>
    IEngineBuilder Packets<TPacket>(Action<IPacketBuilder<TPacket>> map) where TPacket : class, new();

    /// <summary>Sets the application name, used in log headers and as the name of the folder holding the install state. Defaults to the entry assembly's name.</summary>
    IEngineBuilder AppName(string name);

    /// <summary>Sets the application version, shown in the title bar and the info popup. Defaults to the entry assembly's version.</summary>
    IEngineBuilder AppVersion(string version);

    /// <summary>Turns kiosk mode on or off. Kiosk mode hides window chrome and restricts navigation.</summary>
    IEngineBuilder KioskMode(bool enabled = true);

    /// <summary>Sets the text shown in the content area when no entry is selected.</summary>
    IEngineBuilder HomeText(string text);

    /// <summary>Sets the window icon: an <c>avares://</c> URI of an Avalonia asset, or else the path of an image file. Defaults to the operating system's.</summary>
    IEngineBuilder WindowIcon(string path);

    /// <summary>Sets a user name for development and testing, which skips the installed user lookup.</summary>
    IEngineBuilder DebugUser(string userName);

    /// <summary>Sets how an installation code entered by the user resolves to a user name. Defaults to accepting the code <c>CODE</c> for a user named <c>TEST</c>.</summary>
    /// <param name="resolve">Returns the user name for a code, or <see langword="null"/> when the code is not recognized.</param>
    IEngineBuilder UserCodes(Func<string, string?> resolve);

    /// <summary>Adds user names to the directory used for address auto-complete and for connection identification. What is known about each is stated in the network configuration file.</summary>
    IEngineBuilder Users(params string[] names);

    /// <summary>Defines a group of users, whose members may be user names or other group names.</summary>
    IEngineBuilder Group(string name, params string[] members);

    /// <summary>
    /// Defines the ordered set of security levels a message may be sent at, from lowest to highest: each level
    /// ranks higher than the one stated before it. Each level is a display name paired with the hex color shown
    /// for it in the top banner. Empty (the default) turns the whole feature off: every message maps to an empty
    /// security level and no destination is ever blocked for lacking one.
    /// </summary>
    IEngineBuilder SecurityLevels(params (string Name, string Color)[] levels);

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

    /// <summary>Sets which root folders let the user delete entries. Defaults to all of them.</summary>
    IEngineBuilder CanDelete(Func<FolderType, bool> allowed);

    /// <summary>Sets the subject name of the certificate authority trusted to sign every peer's certificate. Defaults to <c>COMLINK-ROOT</c>.</summary>
    IEngineBuilder TrustedAuthority(string certificateName);

    /// <summary>Replaces how the MSMT peer options (identity certificate and trusted authorities) are built, for custom certificate pinning or a non-store certificate source.</summary>
    IEngineBuilder ConnectionOptions(Func<MsmtSessionPeerOptions> options);

    /// <summary>
    /// States the MSMT settings used for every IP connection, inbound and outbound, including the interface listener: timeouts,
    /// keep-alive and session lifetimes. The identity certificate and trusted authorities are still the engine's (or what <see cref="ConnectionOptions"/> builds).
    /// Defaults to the MSMT package defaults.
    /// </summary>
    /// <param name="options">The settings to use.</param>
    IEngineBuilder MsmtOptions(MsmtConnectionOptions options);

    /// <summary>
    /// States the MicroGate options used for every serial connection: line encoding, CRC, clocking, frame size, windowing and
    /// retransmission, which must match the station at the other end of the cable. The HDLC address is not an option; it comes from each serial
    /// connection point. Defaults to the MicroGate defaults.
    /// </summary>
    /// <param name="options">The options to use.</param>
    IEngineBuilder MicroGateOptions(MicroGatePeerOptions options);

    /// <summary>
    /// Sets who is on the other end of a connection that has just formed, by user name. Return <see langword="null"/> to leave it to the engine, which names an
    /// IP connection after the user whose certificate name it carries and a serial connection after the user named on its outgoing point (or else its port).
    /// The app-specific data that travels with the identity is the named user's <see cref="UserInfo.Data"/>.
    /// </summary>
    IEngineBuilder Identify(Func<IConnectionInfo, string?> identify);

    /// <summary>
    /// Sets whether command-line arguments may override where the network configuration file (the file that describes every user
    /// of the network) and the running user come from: <c>--config</c> names the file to read instead of <c>Config.json</c> in the
    /// current working directory, and <c>--user</c> names the user the process runs as instead of <c>User.json</c> in that directory.
    /// The files in the working directory are always read; only the arguments are ignored when this is disallowed.
    /// </summary>
    /// <param name="allowed"><see langword="true"/> to honor <c>--config</c> and <c>--user</c>, <see langword="false"/> to ignore them. Disallowed unless this is called.</param>
    IEngineBuilder CommandLineOverrides(bool allowed);

    /// <summary>Adds an external system, a conduit relaying messages to and from a system outside Comlink.</summary>
    IEngineBuilder ExternalSystem(IExternalSystem system);

    /// <summary>Designates an external system as the exclusive upstream hub every outgoing message is sent to instead of the peer network. It is added like <see cref="ExternalSystem"/> when it is not already.</summary>
    IEngineBuilder ExternalServer(IExternalSystem system);

    /// <summary>
    /// Adds a custom export format, shown as an option alongside the built-in JSON format in the client's export screen (see <see cref="IExportFormat"/>).
    /// </summary>
    /// <typeparam name="TFormat">The format type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder ExportFormat<TFormat>() where TFormat : IExportFormat;

    /// <summary>
    /// Adds a custom import format, shown as an option alongside the built-in package format in the client's import screen (see <see cref="IImportFormat"/>).
    /// </summary>
    /// <typeparam name="TFormat">The format type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder ImportFormat<TFormat>() where TFormat : IImportFormat;
}
