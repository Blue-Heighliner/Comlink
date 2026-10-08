namespace BlueHeighliner.Comlink;

/// <summary>
/// The fluent surface a host uses to say how the engine runs, returned by <see cref="IEngineBuilder.Types{TFrame, TPriority, TLevel, TAspect}"/>, which fixes the frame, packet, priority and message level types every
/// setting is then typed by. Every call returns the builder, and every call is optional except stating the frame handlers with <see cref="Frames"/>: anything left
/// unstated takes the engine's default. Where a setting also exists in the <c>--config</c> file, the file's value wins
/// over what is stated here (see <c>Docs/Components/Config.md</c>). See <c>Docs/Components/Configuration.md</c> for what
/// each group of settings does.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
/// <typeparam name="TPacket">The host's packet type, or <see cref="NoPacket"/> when packets are not used.</typeparam>
/// <typeparam name="TPriority">The enum whose members are the priority levels, or <see cref="NoPriority"/> for a single level.</typeparam>
/// <typeparam name="TLevel">The enum whose members are the message levels, or <see cref="NoMessageLevel"/> for none.</typeparam>
/// <typeparam name="TAspect">The enum whose members are the message aspects, or <see cref="NoMessageAspect"/> for none.</typeparam>
public interface IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> where TFrame : class, new() where TPacket : class, new() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <summary>
    /// Starts stating how the host's frame type, the data format of all network traffic other than packets, is handled: a handler for each kind of frame, and what else depends on the frame type: the print
    /// count, auto forwarders, the network processor and the initial frame processor. A handler for every kind of frame is required. The type must be LiteDB-serializable for
    /// storage, and must satisfy whatever serializer is used for the network (by default protobuf-net, so it needs
    /// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes). Calling it again continues the same statement.
    /// </summary>
    IFrameBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Frames();

    /// <summary>
    /// Turns on packetization and starts stating what depends on the packet type: payloads are broken into prioritized packets of type <typeparamref name="TPacket"/> and
    /// reassembled on the other side, so a large payload does not hold up higher-priority ones, and the initial packet processor. Off by default. Every node on a network must be configured alike, since neither side can tell whether the other packetizes.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TPacket"/> is <see cref="NoPacket"/>.</exception>
    IPacketBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Packets();

    /// <summary>States the handler for the names and words the app shows its users: its name, home text, and labels for concepts such as alerts, tags, priorities and message levels (see <see cref="IDisplayHandler"/>). Every member is optional.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Display<THandler>() where THandler : IDisplayHandler;

    /// <summary>
    /// Starts stating the message levels, members of <typeparamref name="TLevel"/>, from lowest to highest: each level stated ranks higher than the one stated before it, regardless of the order of the enum, and a member
    /// not stated is not a level. The integer values of the members are how levels are stored in drafts, so a member's value must never change or be reused, even when it is no longer used. A level is named by its member name in uppercase, which is how network files refer to it, and shown in the top banner
    /// in a neutral color, unless its aspects say otherwise. A <typeparamref name="TLevel"/> of <see cref="NoMessageLevel"/> turns the whole feature off: every message maps to an empty
    /// message level and no destination is ever blocked for lacking one.
    /// </summary>
    IMessageLevelsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MessageLevels();

    /// <summary>
    /// Starts stating the message aspects, members of <typeparamref name="TAspect"/>, which a message can carry as additional security information beside its message level: a message has one aspect or none, and a member not stated is not an aspect.
    /// The draft view lets the user set one or none, in the order stated. The integer values of the members are how aspects are stored in drafts, so a member's value must never change or be reused, even when it is no longer used. An aspect is named by its member name in uppercase unless a label is stated.
    /// A <typeparamref name="TAspect"/> of <see cref="NoMessageAspect"/> turns the whole feature off.
    /// </summary>
    IMessageAspectsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> MessageAspects();

    /// <summary>States the handler that controls the alarm raised when an alert is received (see <see cref="IAlarmHandler"/>). Defaults to the alarm's own defaults.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Alarms<THandler>() where THandler : IAlarmHandler;

    /// <summary>States the handler that controls how drafts are composed: how wide a line may be and the header a message must start with (see <see cref="IDraftHandler{TPriority, TLevel, TAspect}"/>). Without one lines are not limited and there is no header.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Drafts<THandler>() where THandler : IDraftHandler<TPriority, TLevel, TAspect>;

    /// <summary>
    /// Starts stating the priority levels, members of <typeparamref name="TPriority"/>, lowest first like <see cref="MessageLevels"/>: the position in which a level is stated is its send priority, regardless of the order of the enum, so later levels are sent before earlier ones, and a member not stated is not a level. Required unless the type is <see cref="NoPriority"/>.
    /// The integer values of the members are how levels are stored in drafts and exports, so a member's value must never change or be reused, even when it is no longer used.
    /// A level is named by its member name in uppercase unless its aspects say otherwise; the name is only shown to users.
    /// A level is a <see cref="PriorityMode.User"/> priority, which the GUI offers to users composing a message, unless set to <see cref="PriorityMode.System"/>, which the GUI never offers; code may use any level.
    /// A <typeparamref name="TPriority"/> of <see cref="NoPriority"/> is a single user level named <c>NORMAL</c>. Which tag and priority combinations are blocked when composing a draft is stated here too.
    /// </summary>
    IPriorityBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Priorities();

    /// <summary>Starts configuring the aspects of the address types, such as the display label shown for each in the address type picker, the per-address badge, and the message view's section headers.</summary>
    IAddressTypesBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> AddressTypes();

    /// <summary>States the handler that controls the layout of log lines (see <see cref="ILogHandler"/>). Defaults to no field having a fixed width.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Logs<THandler>() where THandler : ILogHandler;

    /// <summary>States the handler that controls how the print manager behaves (see <see cref="IPrintHandler{TPriority, TLevel, TAspect}"/>). Defaults to the print manager's own defaults.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Prints<THandler>() where THandler : IPrintHandler<TPriority, TLevel, TAspect>;

    /// <summary>States the handler that decides which folders and entries the user may delete (see <see cref="IDeleteHandler"/>). Defaults to allowing everything.</summary>
    /// <typeparam name="THandler">The handler type, instantiated through dependency injection when the engine runs: the instance registered for it in the host's services, or else one constructed from them.</typeparam>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Deletes<THandler>() where THandler : IDeleteHandler;

    /// <summary>Starts configuring how connections are made: the MSMT options of every IP connection and the HDLC options of every serial one.</summary>
    IConnectionsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Connections();


    /// <summary>
    /// Sets whether command-line arguments may override where the network configuration file (the file that describes every user
    /// of the network) and the running user come from: <c>--config</c> names the file to read instead of <c>Config.json</c> in the
    /// current working directory, and <c>--user</c> names the user the process runs as, which is then checked like an installed user, and <c>--log</c> names log categories to turn on beyond the defaults (a comma separated list). The file
    /// in the working directory is always read; only the arguments are ignored when this is disallowed.
    /// </summary>
    /// <param name="allowed"><see langword="true"/> to honor <c>--config</c>, <c>--user</c> and <c>--log</c>, <see langword="false"/> to ignore them. Disallowed unless this is called.</param>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> CommandLineOverrides(bool allowed);

    /// <summary>Adds an external system, a conduit relaying messages to and from a system outside Comlink.</summary>
    IEngineBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> ExternalSystem(IExternalSystem system);

    /// <summary>Starts configuring the export formats of the client's export screen.</summary>
    IExportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Exports();

    /// <summary>Starts configuring the import formats of the client's import screen.</summary>
    IImportsBuilder<TFrame, TPacket, TPriority, TLevel, TAspect> Imports();
}
