namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Implements <see cref="IEngineController"/> against a concrete message type <typeparamref name="TMessage"/>,
/// with sensible hardcoded defaults for every other app area. Message-field members are implemented
/// explicitly (casting <c>object</c> to <typeparamref name="TMessage"/> once on your behalf) and exposed as
/// type-safe <c>protected abstract</c> members instead — a derived class never sees or writes an
/// <c>object</c>-to-<typeparamref name="TMessage"/> cast; see <c>Sample/src/SampleEngineController.cs</c>
/// for a working example. Every other member describes non-config-file behavior; see
/// <see cref="ConfiguredEngineController"/> for how <c>config.json</c> overrides the subset with a
/// corresponding field. Non-abstract members are <see langword="virtual"/> so a host can inherit and
/// override just the ones it actually wants to change — see <c>Docs/Components/Control.md</c>.
/// </summary>
/// <typeparam name="TMessage">
/// The concrete message type. Must be protobuf-net serializable (carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c>
/// attributes) for wire transport, LiteDB-serializable for storage, and have a public parameterless
/// constructor (used by the default <see cref="CreateMessage"/> implementation).
/// </typeparam>
/// <param name="currentUserProvider">Tracks the user name of the currently running instance, read for <see cref="ConnectionOptions"/>.</param>
public abstract class DefaultEngineController<TMessage>(ICurrentUserProvider currentUserProvider) : IEngineController where TMessage : class, new()
{
    private readonly Dictionary<string, IReadOnlyList<string>> emptyGroups = [];
    private readonly List<string> emptyNames = [];
    private readonly Dictionary<string, ServerUserConfig> emptyServerUsers = [];
    private readonly Dictionary<string, string> emptyData = [];
    private readonly List<ConnectionPoint> emptyPoints = [];
    private INetworkSerializer? connectionSerializer;
    private NotSupportedException NoPacketType => new("This engine controller has no packet type; derive from DefaultEngineController<TMessage, TPacket> to enable packetization.");

    /// <inheritdoc cref="IEngineController.MessageType" />
    public Type MessageType => typeof(TMessage);

    /// <inheritdoc cref="IEngineController.NetworkSerializer" />
    public virtual INetworkSerializer NetworkSerializer { get; } = new ProtobufNetworkSerializer(typeof(TMessage));

    /// <inheritdoc cref="IEngineController.PacketSize" />
    public virtual int PacketSize => 16 * 1024;

    /// <inheritdoc cref="IEngineController.PacketWindow" />
    public virtual int PacketWindow => 1;

    /// <summary>Creates a new, empty <typeparamref name="TMessage"/>. The default implementation returns <c>new TMessage()</c>; override for custom construction.</summary>
    protected virtual TMessage CreateMessage() => new();
    /// <summary>Gets the application-level message identifier from <paramref name="message"/>.</summary>
    protected abstract string GetMessageId(TMessage message);
    /// <summary>Sets the application-level message identifier on <paramref name="message"/>.</summary>
    protected abstract void SetMessageId(TMessage message, string value);
    /// <summary>Gets the sender user name from <paramref name="message"/>.</summary>
    protected abstract string GetFromUser(TMessage message);
    /// <summary>Sets the sender user name on <paramref name="message"/>.</summary>
    protected abstract void SetFromUser(TMessage message, string value);
    /// <summary>Gets the subject line from <paramref name="message"/>.</summary>
    protected abstract string GetSubject(TMessage message);
    /// <summary>Sets the subject line on <paramref name="message"/>.</summary>
    protected abstract void SetSubject(TMessage message, string value);
    /// <summary>Gets the body text from <paramref name="message"/>.</summary>
    protected abstract string GetBody(TMessage message);
    /// <summary>Sets the body text on <paramref name="message"/>.</summary>
    protected abstract void SetBody(TMessage message, string value);
    /// <summary>Gets the recipient address list from <paramref name="message"/>.</summary>
    protected abstract List<MessageAddress> GetAddresses(TMessage message);
    /// <summary>Sets the recipient address list on <paramref name="message"/>.</summary>
    protected abstract void SetAddresses(TMessage message, List<MessageAddress> value);
    /// <summary>Gets the UTC sent timestamp from <paramref name="message"/>.</summary>
    protected abstract DateTime GetSentAt(TMessage message);
    /// <summary>Sets the UTC sent timestamp on <paramref name="message"/>.</summary>
    protected abstract void SetSentAt(TMessage message, DateTime value);
    /// <summary>Gets the message ID <paramref name="message"/> is a user-read confirmation for, or an empty string if it is not a confirmation.</summary>
    protected abstract string GetConfirmationMessageId(TMessage message);
    /// <summary>Sets the message ID <paramref name="message"/> is a user-read confirmation for.</summary>
    protected abstract void SetConfirmationMessageId(TMessage message, string value);
    /// <summary>Gets whether <paramref name="message"/> is an alert.</summary>
    protected abstract bool GetIsAlert(TMessage message);
    /// <summary>Sets whether <paramref name="message"/> is an alert.</summary>
    protected abstract void SetIsAlert(TMessage message, bool value);
    /// <summary>Gets the priority number of <paramref name="message"/>.</summary>
    protected abstract int GetPriority(TMessage message);
    /// <summary>Sets the priority number on <paramref name="message"/>.</summary>
    protected abstract void SetPriority(TMessage message, int value);
    /// <summary>Gets the tag identifying the type of <paramref name="message"/>, or an empty string if none was set.</summary>
    protected abstract string GetTag(TMessage message);
    /// <summary>Sets the tag on <paramref name="message"/>.</summary>
    protected abstract void SetTag(TMessage message, string value);

    object IEngineController.CreateMessage() => CreateMessage();
    string IEngineController.GetMessageId(object message) => GetMessageId((TMessage)message);
    void IEngineController.SetMessageId(object message, string value) => SetMessageId((TMessage)message, value);
    string IEngineController.GetFromUser(object message) => GetFromUser((TMessage)message);
    void IEngineController.SetFromUser(object message, string value) => SetFromUser((TMessage)message, value);
    string IEngineController.GetSubject(object message) => GetSubject((TMessage)message);
    void IEngineController.SetSubject(object message, string value) => SetSubject((TMessage)message, value);
    string IEngineController.GetBody(object message) => GetBody((TMessage)message);
    void IEngineController.SetBody(object message, string value) => SetBody((TMessage)message, value);
    List<MessageAddress> IEngineController.GetAddresses(object message) => GetAddresses((TMessage)message);
    void IEngineController.SetAddresses(object message, List<MessageAddress> value) => SetAddresses((TMessage)message, value);
    DateTime IEngineController.GetSentAt(object message) => GetSentAt((TMessage)message);
    void IEngineController.SetSentAt(object message, DateTime value) => SetSentAt((TMessage)message, value);
    string IEngineController.GetConfirmationMessageId(object message) => GetConfirmationMessageId((TMessage)message);
    void IEngineController.SetConfirmationMessageId(object message, string value) => SetConfirmationMessageId((TMessage)message, value);
    bool IEngineController.GetIsAlert(object message) => GetIsAlert((TMessage)message);
    void IEngineController.SetIsAlert(object message, bool value) => SetIsAlert((TMessage)message, value);
    int IEngineController.GetPriority(object message) => GetPriority((TMessage)message);
    void IEngineController.SetPriority(object message, int value) => SetPriority((TMessage)message, value);
    string IEngineController.GetTag(object message) => GetTag((TMessage)message);
    void IEngineController.SetTag(object message, string value) => SetTag((TMessage)message, value);

    Type? IEngineController.PacketType => null;
    INetworkSerializer? IEngineController.PacketSerializer => null;
    object IEngineController.CreatePacket() => throw NoPacketType;
    int IEngineController.GetPayloadId(object packet) => throw NoPacketType;
    void IEngineController.SetPayloadId(object packet, int value) => throw NoPacketType;
    int IEngineController.GetPacketIndex(object packet) => throw NoPacketType;
    void IEngineController.SetPacketIndex(object packet, int value) => throw NoPacketType;
    int IEngineController.GetPacketCount(object packet) => throw NoPacketType;
    void IEngineController.SetPacketCount(object packet, int value) => throw NoPacketType;
    int IEngineController.GetPayloadLength(object packet) => throw NoPacketType;
    void IEngineController.SetPayloadLength(object packet, int value) => throw NoPacketType;
    ReadOnlyMemory<byte> IEngineController.GetPacketData(object packet) => throw NoPacketType;
    void IEngineController.SetPacketData(object packet, ReadOnlyMemory<byte> value) => throw NoPacketType;

    /// <summary>The default <see cref="AppDataPath"/> reads <see cref="AppName"/> through virtual dispatch, so a host overriding only <see cref="AppName"/> automatically gets a matching default data folder.</summary>
    public virtual string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <inheritdoc />
    public virtual string AppVersion => Assembly.GetEntryAssembly()?.GetName().Version is { } version ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    /// <inheritdoc />
    public virtual string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    /// <inheritdoc />
    public virtual bool IsKioskMode => false;
    /// <inheritdoc />
    public virtual string HomeText => "HOME";
    /// <inheritdoc />
    public virtual Uri? WindowIconUri => null;

    /// <inheritdoc />
    public virtual string? DebugUserName => null;
    /// <inheritdoc />
    public virtual IReadOnlyList<string> Users => emptyNames;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, IReadOnlyList<string>> UserGroups => emptyGroups;
    /// <inheritdoc />
    public virtual UserInfo? ResolveCode(string userCode)
        => userCode.Equals("CODE", StringComparison.OrdinalIgnoreCase)
            ? new UserInfo { Name = "TEST", Code = "CODE", EnvironmentTitle = "Test", EnvironmentColor = "#888888" }
            : null;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, string> GetUserData(string userName) => emptyData;
    /// <inheritdoc />
    public virtual UserIdentity? IdentifyConnection(ConnectionInfo connection) => null;
    /// <inheritdoc />
    public virtual object? CreateConnectionMessage(ConnectionInfo connection) => null;
    /// <inheritdoc />
    public virtual object? CreateConnectionResponse(ConnectionInfo connection) => null;

    /// <inheritdoc />
    public virtual int PeerPort => 50021;
    /// <inheritdoc />
    public virtual int InterfacePort => 50020;

    /// <inheritdoc />
    public virtual string AlertLabel => "ALERT";
    /// <inheritdoc />
    public virtual TimeSpan AlarmSoundDuration => TimeSpan.FromSeconds(30);
    /// <inheritdoc />
    public virtual bool QuickConfirmationEnabled => true;
    /// <inheritdoc />
    public virtual bool ComposeAlertsEnabled => true;

    /// <inheritdoc />
    public virtual IReadOnlyList<MessagePriorityOption> Priorities { get; } = [new MessagePriorityOption { Name = "Normal", Value = 0 }];
    /// <inheritdoc />
    public virtual bool TagsEnabled => true;
    /// <inheritdoc />
    public virtual string TagLabel => "Tag";
    /// <inheritdoc />
    public virtual IReadOnlyList<TagPriorityBlock> BlockedCombinations { get; } = [];

    /// <inheritdoc />
    public virtual bool PrintReceivedDefaultEnabled => false;
    /// <summary>Returns how many times <paramref name="message"/> should be automatically added to the print queue when it arrives. The default returns <c>1</c> for every message; override to inspect the message's fields.</summary>
    public virtual int GetPrintCount(TMessage message) => 1;

    int IEngineController.GetPrintCount(object message) => GetPrintCount((TMessage)message);

    /// <inheritdoc />
    public virtual bool CanDelete(FolderType folderType) => true;

    /// <summary>
    /// Builds peer options by looking up the certificates returned by <see cref="GetCertificateName"/> and
    /// <see cref="TrustedAuthorityCertificateName"/> (through virtual dispatch, so overriding just those
    /// members is enough for most customization needs) in the system certificate store. <see
    /// langword="virtual"/> so a host can override the whole policy directly when that is not sufficient —
    /// see <c>Docs/Components/Control.md</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No current user is registered yet, so no identity certificate can be resolved.</exception>
    public virtual MsmtSessionPeerOptions ConnectionOptions => MsmtCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName, TrustedAuthorityCertificateName);

    /// <inheritdoc />
    public virtual NodeRole Role => NodeRole.Peer;
    /// <inheritdoc />
    public virtual IReadOnlyList<ConnectionPoint> OutgoingPoints => emptyPoints;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, ServerUserConfig> Servers => emptyServerUsers;
    /// <inheritdoc />
    public virtual Type? ConnectionMessageType => null;
    /// <inheritdoc />
    public virtual Type? ConnectionResponseType => null;
    /// <inheritdoc />
    public virtual INetworkSerializer? ConnectionSerializer => ConnectionMessageType is null
        ? null
        : connectionSerializer ??= new ProtobufNetworkSerializer([.. new[] { ConnectionMessageType, ConnectionResponseType }.OfType<Type>()]);

    /// <inheritdoc />
    public virtual bool ConfigFileEnabled => false;

    /// <summary>
    /// The external systems this instance communicates with — each a conduit relaying messages to and from
    /// another system outside Comlink. <see cref="ExternalSystemBase{TMessage}"/> is available as an
    /// optional convenience base class for implementing one (see <c>Docs/Components/ExternalSystems.md</c>), but is
    /// not required — any <see cref="IExternalSystem"/> implementation works here. Read once at startup by
    /// <see cref="ExternalSystemsService"/>: every message this instance receives (from a peer, or from any
    /// other external system) is sent through every other external system in the returned list, and every
    /// message received from one is processed exactly like an ordinary received message. The default is an
    /// empty list — override to provide one or more.
    /// </summary>
    public virtual IReadOnlyList<IExternalSystem> ExternalSystems { get; } = [];

    /// <summary>
    /// The single external system, from <see cref="ExternalSystems"/>, that should exclusively receive
    /// every message this instance would otherwise send out. The default is <see langword="null"/>,
    /// disabling this gateway behavior — override to designate one of <see cref="ExternalSystems"/>'s own
    /// entries as the exclusive upstream hub.
    /// </summary>
    public virtual IExternalSystem? ExternalServer { get; } = null;

    /// <inheritdoc />
    public virtual string GetCertificateName(string userName) => userName;

    /// <inheritdoc />
    public virtual string TrustedAuthorityCertificateName => "COMLINK-ROOT";
}

/// <summary>
/// A <see cref="DefaultEngineController{TMessage}"/> that also enables packetization: payloads are broken into
/// prioritized packets of type <typeparamref name="TPacket"/> and reassembled on the other side, so a large
/// payload does not hold up higher-priority ones queued behind it. The engine does all of the splitting and
/// reassembling itself; a host only says what a packet looks like, by implementing the abstract members that get
/// and set the fields the engine needs in a <typeparamref name="TPacket"/> and, if it wants a different wire
/// format, overriding <see cref="PacketSerializer"/> the way <see cref="DefaultEngineController{TMessage}.NetworkSerializer"/>
/// is overridden for messages. Every node this instance talks to must derive from this class too, since neither
/// side can tell whether the other packetizes.
/// </summary>
/// <typeparam name="TMessage">The host's message type; see <see cref="DefaultEngineController{TMessage}"/>.</typeparam>
/// <typeparam name="TPacket">
/// The host's packet type: serializable by <see cref="PacketSerializer"/> (by default protobuf-net, so carrying
/// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes) and with a public parameterless constructor, used by
/// the default <see cref="CreatePacket"/> implementation.
/// </typeparam>
/// <param name="currentUserProvider">Tracks the user name of the currently running instance; see <see cref="DefaultEngineController{TMessage}"/>.</param>
public abstract class DefaultEngineController<TMessage, TPacket>(ICurrentUserProvider currentUserProvider) : DefaultEngineController<TMessage>(currentUserProvider), IEngineController
    where TMessage : class, new()
    where TPacket : class, new()
{
    /// <inheritdoc cref="IEngineController.PacketSerializer" />
    public virtual INetworkSerializer PacketSerializer { get; } = new ProtobufNetworkSerializer(typeof(TPacket));

    /// <summary>Creates a new, empty <typeparamref name="TPacket"/>. The default implementation returns <c>new TPacket()</c>; override for custom construction.</summary>
    protected virtual TPacket CreatePacket() => new();
    /// <summary>Gets the identifier shared by every packet of one payload.</summary>
    protected abstract int GetPayloadId(TPacket packet);
    /// <summary>Sets the payload identifier on <paramref name="packet"/>.</summary>
    protected abstract void SetPayloadId(TPacket packet, int value);
    /// <summary>Gets the zero-based position of <paramref name="packet"/> among the packets of its payload.</summary>
    protected abstract int GetPacketIndex(TPacket packet);
    /// <summary>Sets the position of <paramref name="packet"/> among the packets of its payload.</summary>
    protected abstract void SetPacketIndex(TPacket packet, int value);
    /// <summary>Gets how many packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    protected abstract int GetPacketCount(TPacket packet);
    /// <summary>Sets the number of packets the payload <paramref name="packet"/> belongs to was broken into.</summary>
    protected abstract void SetPacketCount(TPacket packet, int value);
    /// <summary>Gets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    protected abstract int GetPayloadLength(TPacket packet);
    /// <summary>Sets the length in bytes of the whole payload <paramref name="packet"/> belongs to.</summary>
    protected abstract void SetPayloadLength(TPacket packet, int value);
    /// <summary>Gets the slice of the payload <paramref name="packet"/> carries.</summary>
    protected abstract ReadOnlyMemory<byte> GetPacketData(TPacket packet);
    /// <summary>Sets the slice of the payload <paramref name="packet"/> carries. <paramref name="value"/> is only valid for the duration of the call, so a packet that stores it must copy it.</summary>
    protected abstract void SetPacketData(TPacket packet, ReadOnlyMemory<byte> value);

    Type? IEngineController.PacketType => typeof(TPacket);
    INetworkSerializer? IEngineController.PacketSerializer => PacketSerializer;
    object IEngineController.CreatePacket() => CreatePacket();
    int IEngineController.GetPayloadId(object packet) => GetPayloadId((TPacket)packet);
    void IEngineController.SetPayloadId(object packet, int value) => SetPayloadId((TPacket)packet, value);
    int IEngineController.GetPacketIndex(object packet) => GetPacketIndex((TPacket)packet);
    void IEngineController.SetPacketIndex(object packet, int value) => SetPacketIndex((TPacket)packet, value);
    int IEngineController.GetPacketCount(object packet) => GetPacketCount((TPacket)packet);
    void IEngineController.SetPacketCount(object packet, int value) => SetPacketCount((TPacket)packet, value);
    int IEngineController.GetPayloadLength(object packet) => GetPayloadLength((TPacket)packet);
    void IEngineController.SetPayloadLength(object packet, int value) => SetPayloadLength((TPacket)packet, value);
    ReadOnlyMemory<byte> IEngineController.GetPacketData(object packet) => GetPacketData((TPacket)packet);
    void IEngineController.SetPacketData(object packet, ReadOnlyMemory<byte> value) => SetPacketData((TPacket)packet, value);
}
