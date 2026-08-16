namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Single control interface consolidating every extension point through which a host application
/// customises Engine behaviour without modifying Engine code: the concrete message type and its logical
/// field mapping, app identity/presentation, local user identity, the user/group directory, listener
/// ports, alert settings, message composition, the automatic print policy, OFT peer certificate naming
/// and peer options, network topology, the external systems this instance communicates with, and whether
/// <c>config.json</c> is read at all. External drive discovery and printer discovery/driving are real
/// OS-level behavior, not configuration or rules, so they live on <see cref="Devices.IExternalDriveProvider"/>
/// and <see cref="Devices.IPrintDriver"/> instead. See <c>Docs/Control.md</c>.
/// </summary>
public interface IEngineController
{
    /// <summary>
    /// The concrete message type used throughout the engine. Must be protobuf-net serializable (carry
    /// <c>[ProtoContract]</c>/<c>[ProtoMember]</c> attributes) for wire transport, and must be a type
    /// LiteDB can serialize for storage.
    /// </summary>
    Type MessageType { get; }

    /// <summary>The application name, used as the default data folder name and in log headers.</summary>
    string AppName { get; }
    /// <summary>Absolute path to the application data directory.</summary>
    string AppDataPath { get; }
    /// <summary><see langword="true"/> to enable kiosk mode, which hides window chrome and restricts navigation.</summary>
    bool IsKioskMode { get; }
    /// <summary>The text displayed in the content area when no entry is selected.</summary>
    string HomeText { get; }

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

    /// <summary>The peer options — including TLS certificate, validation, and security mode — used for both inbound and outbound OFT peer connections.</summary>
    OftPeerOptions ConnectionOptions { get; }

    /// <summary>The configured role for this instance.</summary>
    NodeRole Role { get; }
    /// <summary>
    /// The server endpoint a <see cref="NodeRole.Client"/> instance forms its single long-term
    /// connection to, or <see langword="null"/> if none is configured. Unused outside <see cref="NodeRole.Client"/>.
    /// </summary>
    UserEndpoint? ServerEndpoint { get; }
    /// <summary>
    /// The full server-user map a <see cref="NodeRole.Server"/> instance routes with, keyed by server user
    /// name (case-insensitive) — every server in the cluster, not just the local one. Unused outside
    /// <see cref="NodeRole.Server"/>.
    /// </summary>
    IReadOnlyDictionary<string, ServerUserConfig> Servers { get; }

    /// <summary>When <see langword="true"/>, a <c>--config</c> argument is read; when <see langword="false"/> (the default), it is ignored and <see cref="EngineConfig"/> always uses its defaults.</summary>
    bool ConfigFileEnabled { get; }

    /// <summary>
    /// The external systems this instance communicates with — each a conduit relaying messages to and
    /// from a system outside Comlink (a socket, a message queue, an HTTP long-poll, etc.). Resolved once
    /// at startup by <c>ExternalSystemsService</c>: every message this instance receives (from a peer, or
    /// from any other external system) is sent through every other external system in this list, and
    /// every message received from one is processed exactly like an ordinary received message. See
    /// <c>Docs/ExternalSystems.md</c>.
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
    /// as the exclusive upstream hub. See <c>Docs/ExternalSystems.md</c>.
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
    /// <c>Docs/Peer.md</c>.
    /// </summary>
    string GetConfirmationMessageId(object message);
    /// <summary>Sets the message ID <paramref name="message"/> is a user-read confirmation for.</summary>
    void SetConfirmationMessageId(object message, string value);
    /// <summary>
    /// Gets whether <paramref name="message"/> is an alert: an ordinary message that also causes the
    /// receiving Client-mode UI to alarm (visually and audibly) until the user reads it. See
    /// <c>Docs/ViewModels.md</c>.
    /// </summary>
    bool GetIsAlert(object message);
    /// <summary>Sets whether <paramref name="message"/> is an alert.</summary>
    void SetIsAlert(object message, bool value);
    /// <summary>
    /// Gets the priority number of <paramref name="message"/>. One of the values returned by
    /// <see cref="Priorities"/>; used verbatim as the OFT send priority (larger values are sent first —
    /// see <c>Docs/Peer.md</c>) whenever this message is sent over an OFT connection.
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

    /// <summary>Resolves <paramref name="userCode"/> to its <see cref="UserInfo"/>, or <see langword="null"/> if the code is unrecognized.</summary>
    /// <param name="userCode">The user installation code to resolve.</param>
    UserInfo? ResolveCode(string userCode);
    /// <summary>Returns the TCP endpoint for <paramref name="userName"/>, or <see langword="null"/> if the user is unknown.</summary>
    /// <param name="userName">The user name to resolve.</param>
    UserEndpoint? GetEndpoint(string userName);

    /// <summary>
    /// Returns how many times <paramref name="message"/> should be automatically added to the print queue
    /// when it arrives — <c>0</c> to not print it, <c>1</c> to print it once, <c>2</c> to print two copies, and
    /// so on. Only consulted while the print manager's "print received" toggle is enabled.
    /// </summary>
    /// <param name="message">The received message, in this instance's own <see cref="MessageType"/>.</param>
    int GetPrintCount(object message);

    /// <summary>
    /// Returns the certificate subject name to search for in the system store for the given user.
    /// Returns <see langword="null"/> to disable peer authentication (unauthenticated mode).
    /// When a non-null name is returned and no matching certificate exists, startup throws.
    /// </summary>
    /// <param name="userName">The local user name for which to resolve a certificate name.</param>
    string? GetCertificateName(string userName);
}

/// <summary>
/// Implements <see cref="IEngineController"/> against a concrete message type <typeparamref name="TMessage"/>,
/// with sensible hardcoded defaults for every other app area. Message-field members are implemented
/// explicitly (casting <c>object</c> to <typeparamref name="TMessage"/> once on your behalf) and exposed as
/// type-safe <c>protected abstract</c> members instead — a derived class never sees or writes an
/// <c>object</c>-to-<typeparamref name="TMessage"/> cast; see <c>Sample/src/SampleEngineController.cs</c>
/// for a working example. Every other member describes non-config-file behavior; see
/// <see cref="ConfiguredEngineController"/> for how <c>config.json</c> overrides the subset with a
/// corresponding field. Non-abstract members are <see langword="virtual"/> so a host can inherit and
/// override just the ones it actually wants to change — see <c>Docs/Control.md</c>.
/// </summary>
/// <typeparam name="TMessage">
/// The concrete message type. Must be protobuf-net serializable (carry <c>[ProtoContract]</c>/<c>[ProtoMember]</c>
/// attributes) for wire transport, LiteDB-serializable for storage, and have a public parameterless
/// constructor (used by the default <see cref="CreateMessage"/> implementation).
/// </typeparam>
public abstract class DefaultEngineController<TMessage> : IEngineController where TMessage : class, new()
{
    /// <summary>Initializes a new instance reading the current user from <paramref name="currentUserProvider"/> for <see cref="ConnectionOptions"/>.</summary>
    /// <param name="currentUserProvider">Tracks the user name of the currently running instance.</param>
    protected DefaultEngineController(ICurrentUserProvider currentUserProvider)
        => this.currentUserProvider = currentUserProvider;

    private readonly ICurrentUserProvider currentUserProvider;
    private readonly Dictionary<string, IReadOnlyList<string>> emptyGroups = [];
    private readonly List<string> emptyNames = [];
    private readonly Dictionary<string, ServerUserConfig> emptyServerUsers = [];

    /// <inheritdoc cref="IEngineController.MessageType" />
    public Type MessageType => typeof(TMessage);

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

    /// <summary>The default <see cref="AppDataPath"/> reads <see cref="AppName"/> through virtual dispatch, so a host overriding only <see cref="AppName"/> automatically gets a matching default data folder.</summary>
    public virtual string AppName => Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
    /// <inheritdoc />
    public virtual string AppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
    /// <inheritdoc />
    public virtual bool IsKioskMode => false;
    /// <inheritdoc />
    public virtual string HomeText => "HOME";

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
    public virtual UserEndpoint? GetEndpoint(string userName) => null;

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

    /// <summary>
    /// Builds peer options by looking up the certificate returned by <see cref="GetCertificateName"/> (through
    /// virtual dispatch, so overriding just that member is enough for most customization needs) in the system
    /// certificate store. <see langword="virtual"/> so a host can override the whole policy directly when that
    /// is not sufficient — see <c>Docs/Control.md</c>.
    /// </summary>
    public virtual OftPeerOptions ConnectionOptions => OftCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName);

    /// <inheritdoc />
    public virtual NodeRole Role => NodeRole.Peer;
    /// <inheritdoc />
    public virtual UserEndpoint? ServerEndpoint => null;
    /// <inheritdoc />
    public virtual IReadOnlyDictionary<string, ServerUserConfig> Servers => emptyServerUsers;

    /// <inheritdoc />
    public virtual bool ConfigFileEnabled => false;

    /// <summary>
    /// The external systems this instance communicates with — each a conduit relaying messages to and from
    /// another system outside Comlink. <see cref="ExternalSystemBase{TMessage}"/> is available as an
    /// optional convenience base class for implementing one (see <c>Docs/ExternalSystems.md</c>), but is
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
    public virtual string? GetCertificateName(string userName) => $"USER-{userName}";
}

/// <summary>
/// Shared certificate store lookup for <see cref="DefaultEngineController{TMessage}.ConnectionOptions"/> and
/// <see cref="ConfiguredEngineController.ConnectionOptions"/> — not itself generic over the message type, since
/// certificate lookup has nothing to do with it.
/// </summary>
internal static class OftCertificateLookup
{
    /// <summary>
    /// Looks up <paramref name="currentUserName"/>'s certificate via <paramref name="getCertificateName"/>
    /// (each caller passes its own, potentially config-overridden, method) in the system certificate store.
    /// </summary>
    public static OftPeerOptions BuildPeerOptions(string? currentUserName, Func<string, string?> getCertificateName)
    {
        X509Certificate2? cert = GetOwnCertificate(currentUserName, getCertificateName);
        return new OftPeerOptions
        {
            Info = currentUserName ?? string.Empty,
            Certificate = cert,
            CertificateValidation = cert is not null ? ValidateChain : null,
            SecurityMode = cert is not null ? OftSecurityMode.DualAuthentication : OftSecurityMode.Secure
        };
    }

    private static X509Certificate2? GetOwnCertificate(string? userName, Func<string, string?> getCertificateName)
    {
        if (userName is null) { return null; }
        string? certName = getCertificateName(userName);
        if (certName is null) { return null; }
        return FindCertificate(certName)
            ?? throw new InvalidOperationException(
                $"Peer authentication is required but no certificate named '{certName}' was found in the system store. " +
                "Install the certificate or set PeerCertificateName to \"disable\" to run without authentication.");
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

    private static bool ValidateChain(object _, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        => errors == SslPolicyErrors.None;
}

/// <summary>
/// Engine-level decorator applying every <c>config.json</c> field over whichever <see cref="IEngineController"/>
/// is registered (a host's <see cref="DefaultEngineController{TMessage}"/> subclass) — field by field, for
/// just the members that have a corresponding <c>config.json</c> field; every other member, including the
/// entire message-format surface, delegates straight to the wrapped provider. Registered by
/// <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
/// <see cref="ConnectionOptions"/> has no <c>config.json</c> field of its own but is reimplemented (rather than
/// delegated) so it consumes this decorator's own, potentially config-overridden, <see cref="GetCertificateName"/>
/// instead of the wrapped provider's raw one.
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
        _endpoints = config.GetUserEndpoints();
    }

    private readonly IEngineController fallback;
    private readonly EngineConfig config;
    private readonly ICurrentUserProvider currentUserProvider;
    private readonly IReadOnlyDictionary<string, UserEndpoint> _endpoints;

    /// <inheritdoc />
    public Type MessageType => fallback.MessageType;
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
    public string AppName => fallback.AppName;
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
    public OftPeerOptions ConnectionOptions => OftCertificateLookup.BuildPeerOptions(currentUserProvider.UserName, GetCertificateName);

    /// <inheritdoc />
    public NodeRole Role =>
        config.NodeRole is not null && Enum.TryParse(config.NodeRole, ignoreCase: true, out NodeRole role)
            ? role
            : fallback.Role;

    /// <inheritdoc />
    public UserEndpoint? ServerEndpoint =>
        config.ServerEndpoint is { } endpoint
            ? new UserEndpoint { IpAddress = endpoint.IpAddress, Port = endpoint.Port }
            : fallback.ServerEndpoint;

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
    public bool ConfigFileEnabled => fallback.ConfigFileEnabled;

    /// <inheritdoc />
    public IReadOnlyList<IExternalSystem> ExternalSystems => fallback.ExternalSystems;

    /// <inheritdoc />
    public IExternalSystem? ExternalServer => fallback.ExternalServer;

    /// <inheritdoc />
    public UserInfo? ResolveCode(string userCode) => fallback.ResolveCode(userCode);
    /// <inheritdoc />
    public UserEndpoint? GetEndpoint(string userName)
        => _endpoints.TryGetValue(userName, out UserEndpoint? endpoint) ? endpoint : fallback.GetEndpoint(userName);
    /// <inheritdoc />
    public string? GetCertificateName(string userName)
        => config.PeerCertificateName switch
        {
            null => fallback.GetCertificateName(userName),
            "disable" => null,
            string name => name
        };
}
