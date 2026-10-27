namespace BlueHeighliner.Comlink;

/// <summary>
/// The fluent surface a host uses to say how the engine runs, returned by <see cref="IEngineBuilder.Types{TFrame, TPriority, TLevel, TAspect, TProfile}"/>, which fixes the frame, packet, priority and level types every
/// setting is then typed by. Every call returns the builder, and every call is optional except stating the frame handlers with <see cref="Frames"/>: anything left
/// unstated takes the engine's default. Where a setting also exists in the <c>--config</c> file, the file's value wins
/// over what is stated here (see <c>Docs/Components/Config.md</c>). See <c>Docs/Components/Configuration.md</c> for what
/// each group of settings does.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/> when packets are not used.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels, or <see cref="NoPriority"/> for a single level.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the levels, or <see cref="NoLevel"/> for none.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the aspects, or <see cref="NoAspect"/> for none.</typeparam>
/// <typeparam name="TProfile">The record type the host stated for user profiles in its <c>Types</c> call, or <see cref="NoProfile"/>.</typeparam>
public interface IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum where TProfile : class
{
    /// <summary>
    /// Adds an auto forwarder called <paramref name="name"/>, shown as an option in the client's auto forward screen to every user whose <c>AutoForwarders</c> in the network configuration file lists the name, each of whom keeps their own
    /// target list there. The engine only keeps the lists; what an auto forwarder accepts and the forwarding are the handler's (see <see cref="INetwork{TFrame, TPriority, TLevel, TAspect}.GetAutoForwardTargets"/>).
    /// Stating a name that is already an auto forwarder changes nothing.
    /// </summary>
    /// <param name="name">The auto forwarder's name, which is also the key of its target lists.</param>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> AutoForwarder(string name);

    /// <summary>
    /// Starts stating how the host's frame type, the data format of all network traffic other than packets, is handled: a handler for each kind of frame, and what else depends on the frame type: the print
    /// count and auto forwarders. A handler for every kind of frame is required. The type must be LiteDB-serializable for
    /// storage, and must satisfy whatever serializer is used for the network (by default protobuf-net, so it needs
    /// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes). Calling it again continues the same statement and replaces the handler.
    /// </summary>
    /// <typeparam name="THandler">The handler that carries out the host's protocol: it reacts to a user connecting or disconnecting, a frame being received, the user sending a message, reading one or submitting a retrieval (see <see cref="IFrameHandler{TFrame, TPriority, TLevel, TAspect}"/>). The engine receives and sends nothing itself. Instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Frames<THandler>() where THandler : IFrameHandler<TFrame, TPriority, TLevel, TAspect>;

    /// <summary>
    /// Turns on packetization and starts stating what depends on the packet type: payloads are broken into prioritized packets of type <typeparamref name="TPacket"/> and
    /// reassembled on the other side, so a large payload does not hold up higher-priority ones, and the handshake handler. Off by default. Every node on a network must be configured alike, since neither side can tell whether the other packetizes.
    /// </summary>
    /// <typeparam name="THandler">The handler for the packets that carry a piece of a serialized frame (see <see cref="IPacketHandler{TFrame, TPacket}"/>), instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    /// <param name="maxPayloadSize">The largest slice of a serialized frame a packet carries, in bytes. Smaller payloads let a higher-priority frame cut in sooner; larger ones carry less framing overhead. It limits the payload only: the packet's own fields (its frame id, index, count and length, and whatever else the packet type and serializer add) come on top of it, so a connection that limits what it can carry (the HDLC <c>MaxInfoField</c>, say) needs a maximum that leaves room for them. A packet that does not fit the connection fails to send, and the reason is logged.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TPacket"/> is <see cref="NoPacket"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPayloadSize"/> is less than 1.</exception>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Packets<THandler>(int maxPayloadSize) where THandler : IPacketHandler<TFrame, TPacket>;

    /// <summary>States the handler for the names and words the app shows its users: its name, home text, and labels for concepts such as alerts, tags, priorities and levels (see <see cref="IDisplayHandler"/>). Every member is optional.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Display<THandler>() where THandler : IDisplayHandler;

    /// <summary>
    /// States one level, a member of <typeparamref name="TLevel"/>; state the levels from lowest to highest: each level stated ranks higher than the one stated before it, regardless of the order of the enum, and a member
    /// not stated is not a level. The integer values of the members are how levels are stored in drafts, so a member's value must never change or be reused, even when it is no longer used. A level is named by its member name in uppercase, which is how network files refer to it, and shown in the top banner
    /// in a neutral color, unless its aspects say otherwise. A <typeparamref name="TLevel"/> of <see cref="NoLevel"/> turns the whole feature off: every message maps to an empty
    /// level and no destination is ever blocked for lacking one.
    /// </summary>
    ILevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Level(TLevel level);

    /// <summary>
    /// States one aspect, a member of <typeparamref name="TAspect"/>; the aspects are what a message can carry as additional security information beside its level: a message has one aspect or none, and a member not stated is not an aspect.
    /// The draft view lets the user set one or none, in the order stated. The integer values of the members are how aspects are stored in drafts, so a member's value must never change or be reused, even when it is no longer used. An aspect is named by its member name in uppercase unless a label is stated.
    /// A <typeparamref name="TAspect"/> of <see cref="NoAspect"/> turns the whole feature off.
    /// </summary>
    IAspectBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Aspect(TAspect aspect);

    /// <summary>States the handler that controls the alarm raised when an alert is received (see <see cref="IAlarmHandler"/>). Defaults to the alarm's own defaults.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Alarms<THandler>() where THandler : IAlarmHandler;

    /// <summary>States the handler that controls how drafts are composed: how wide a line may be and the header a message must start with (see <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/>). Without one lines are not limited and there is no header.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Drafts<THandler>() where THandler : IDraftHandler<TPriority, TLevel, TAspect>;

    /// <summary>
    /// States one priority level, members of <typeparamref name="TPriority"/>, lowest first like <see cref="Level"/>: the position in which a level is stated is its send priority, regardless of the order of the enum, so later levels are sent before earlier ones, and a member not stated is not a level. Required unless the type is <see cref="NoPriority"/>.
    /// The integer values of the members are how levels are stored in drafts and exports, so a member's value must never change or be reused, even when it is no longer used.
    /// A level is named by its member name in uppercase unless its aspects say otherwise; the name is only shown to users.
    /// A level is offered by the GUI to users composing a message unless it is hidden with <c>IsHidden()</c>, which the GUI never offers; code may use any level.
    /// A <typeparamref name="TPriority"/> of <see cref="NoPriority"/> is a single user level named <c>NORMAL</c>. Which tag and priority combinations are blocked when composing a draft is stated here too.
    /// </summary>
    IPriorityLevelBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Priority(TPriority priority);

    /// <summary>Selects an address type to configure, such as the display label shown for each in the address type picker, the per-address badge, and the message view's section headers.</summary>
    IAddressTypeBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> AddressType(AddressType type);

    /// <summary>States the handler that controls the layout of log lines (see <see cref="ILogHandler"/>). Defaults to no field having a fixed width.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Logs<THandler>() where THandler : ILogHandler;

    /// <summary>States the handler that controls how the print manager behaves (see <see cref="IPrintHandler{TPriority, TLevel, TAspect}"/>). Defaults to the print manager's own defaults.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Prints<THandler>() where THandler : IPrintHandler<TPriority, TLevel, TAspect>;

    /// <summary>States the handler that decides which folders and entries the user may delete (see <see cref="IDeleteHandler"/>). Defaults to allowing everything.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Deletes<THandler>() where THandler : IDeleteHandler;

    /// <summary>Starts configuring the MSMT settings of every IP connection (see <see cref="IMsmtBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>). Calling it again continues the same statement.</summary>
    IMsmtBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Msmt();

    /// <summary>Starts configuring the HDLC options of every serial connection (see <see cref="IHdlcBuilder{TFrame, TPacket, TPriority, TLevel, TAspect, TProfile}"/>). Calling it again continues the same statement.</summary>
    IHdlcBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Hdlc();


    /// <summary>
    /// Sets the certificate files the node authenticates with. The user the node runs as is the subject common name of the user certificate, so there is nothing to install or name.
    /// Either is required unless the command line (see <see cref="CommandLineOverrides"/>) supplies it, and the node does not start without both.
    /// </summary>
    /// <param name="authorityPath">The public certificate file (for example <c>.cer</c>) of the certificate authority trusted to sign every user certificate, the only authority trusted. A relative path is relative to the app's folder under the application data root.</param>
    /// <param name="userPath">The PKCS#12 (<c>.pfx</c>) file with the user's identity certificate and private key, which must chain to the authority certificate and be issued to a user of the network. A relative path is relative to the app's folder under the application data root.</param>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Certificates(string authorityPath, string userPath);

    /// <summary>
    /// Sets whether a log entry that reports an exception also carries the exception's entire stack trace. On by default. When turned off such an entry carries only the exception's type and message, and those of the exceptions inside it.
    /// It applies to every entry that reports an exception, the engine's own and those written through <see cref="ILogger"/>. A host that wants traces only while it is being developed can ask for the <see cref="IDebugMode"/> in its configuration's constructor and pass <see cref="IDebugMode.IsDebug"/>.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to log stack traces, <see langword="false"/> not to.</param>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> StackTrace(bool enabled);

    /// <summary>
    /// States the type that takes part in the engine's configuration: it says which user a certificate means and is called each time the configuration has been loaded and the current user identified (see <see cref="IConfigurationHandler"/>). Only one can be stated; a later call replaces an earlier one.
    /// </summary>
    /// <typeparam name="TConfigurationHandler">The type to build through dependency injection and call.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Configure<TConfigurationHandler>() where TConfigurationHandler : class, IConfigurationHandler;

    /// <summary>
    /// Sets whether command-line arguments may override what the host stated: <c>--config</c> names the network configuration file to read instead of <c>Config.json</c> in the
    /// app's data folder, <c>--data</c> names the app's data folder (the network configuration file, the database, the logs and the stored records) instead of the app's folder under the application data root, which is how several nodes run on one machine, each with its own,
    /// <c>--authority</c> and <c>--certificate</c> name the authority and user certificate files instead of those of <see cref="Certificates"/>,
    /// and <c>--log</c> names log categories to turn on beyond the defaults (a comma separated list).
    /// The file in the app data folder is always read; only the arguments are ignored when this is disallowed.
    /// </summary>
    /// <param name="allowed"><see langword="true"/> to honor <c>--config</c>, <c>--authority</c>, <c>--certificate</c>, <c>--data</c> and <c>--log</c>, <see langword="false"/> to ignore them. Disallowed unless this is called.</param>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> CommandLineOverrides(bool allowed);

    /// <summary>Adds an external system, a conduit relaying messages to and from a system outside Comlink.</summary>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> ExternalSystem(IExternalSystem system);

    /// <summary>Adds a custom export format, shown as an option alongside the built-in JSON format in the client's export screen (see <see cref="IExportFormat"/>). Adding another with the same name replaces the earlier one in place.</summary>
    /// <typeparam name="TFormat">The format type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Export<TFormat>() where TFormat : IExportFormat;

    /// <summary>Adds a custom import format, shown as an option alongside the built-in package format in the client's import screen (see <see cref="IImportFormat{TPriority, TLevel}"/>). Adding another with the same name replaces the earlier one in place.</summary>
    /// <typeparam name="TFormat">The format type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect, TProfile> Import<TFormat>() where TFormat : IImportFormat<TPriority, TLevel>;
}
