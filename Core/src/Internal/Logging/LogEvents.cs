namespace BlueHeighliner.Comlink;

/// <summary>
/// Every event the engine logs, each with an identifier that is unique and never reused. The identifier is written in the log output as a bracketed element
/// and is listed with each entry of the activity log; <c>Docs/Components/Logging.md</c> describes every event.
/// </summary>
internal static class LogEvents
{
    private static Dictionary<int, string> Categories { get; } = [];

    /// <summary>An exception reaches the top-level handler of the application.</summary>
    public static EventId UnhandledException { get; } = Define(1, nameof(UnhandledException), LogCategories.Crash);

    /// <summary>The role's peer service cannot be built, for example because a certificate file is missing.</summary>
    public static EventId NetworkingCouldNotStart { get; } = Define(2, nameof(NetworkingCouldNotStart), LogCategories.Error);

    /// <summary>The network configuration file is invalid for the role: a client or relay has no parent (at start or after a reload), or a server is missing from its own server map.</summary>
    public static EventId InvalidConfigurationFile { get; } = Define(3, nameof(InvalidConfigurationFile), LogCategories.Error);

    /// <summary>The local interface listener cannot be built, for example because a certificate file is missing.</summary>
    public static EventId InterfaceCannotStart { get; } = Define(8, nameof(InterfaceCannotStart), LogCategories.Error);

    /// <summary>A payload is too large for the connection it is sent over, whether MSMT, the packetizer or an HDLC frame.</summary>
    public static EventId PayloadTooLarge { get; } = Define(11, nameof(PayloadTooLarge), LogCategories.Error);

    /// <summary>A message received from a peer, a server, a relay, the interface or an external system is invalid and is dropped.</summary>
    public static EventId InvalidMessage { get; } = Define(14, nameof(InvalidMessage), LogCategories.Error);

    /// <summary>Routing, forwarding or relaying a message throws.</summary>
    public static EventId RouteFailed { get; } = Define(19, nameof(RouteFailed), LogCategories.Error);

    /// <summary>A handler of a user connecting or disconnecting throws.</summary>
    public static EventId ConnectionEventHandlerFailed { get; } = Define(22, nameof(ConnectionEventHandlerFailed), LogCategories.Error);

    /// <summary>Storing a message received while the UI runs throws.</summary>
    public static EventId StoreReceivedMessageFailed { get; } = Define(23, nameof(StoreReceivedMessageFailed), LogCategories.Error);

    /// <summary>A storage server fails to save a message it relayed.</summary>
    public static EventId StoreMessageCopyFailed { get; } = Define(24, nameof(StoreMessageCopyFailed), LogCategories.Error);

    /// <summary>A storage server fails to read messages for a retrieval request.</summary>
    public static EventId ReadStoredMessagesFailed { get; } = Define(25, nameof(ReadStoredMessagesFailed), LogCategories.Error);

    /// <summary>Sending a draft or a staged message throws.</summary>
    public static EventId SendFailed { get; } = Define(26, nameof(SendFailed), LogCategories.Error);

    /// <summary>The automatic first store of a new draft throws.</summary>
    public static EventId StoreNewDraftFailed { get; } = Define(28, nameof(StoreNewDraftFailed), LogCategories.Error);

    /// <summary>Selecting the folder and entry of what was opened throws.</summary>
    public static EventId RevealOpenedEntryFailed { get; } = Define(29, nameof(RevealOpenedEntryFailed), LogCategories.Error);

    /// <summary>Saving a draft or note when leaving it throws.</summary>
    public static EventId SaveOnLeavingFailed { get; } = Define(30, nameof(SaveOnLeavingFailed), LogCategories.Error);

    /// <summary>An import finds a message or draft with a priority that is not configured.</summary>
    public static EventId ImportPriorityRejected { get; } = Define(31, nameof(ImportPriorityRejected), LogCategories.Activity);

    /// <summary>Reading the content of a print job throws.</summary>
    public static EventId PrintContentLoadFailed { get; } = Define(33, nameof(PrintContentLoadFailed), LogCategories.Error);

    /// <summary>Sending a print job to the printer throws.</summary>
    public static EventId PrintFailed { get; } = Define(34, nameof(PrintFailed), LogCategories.Error);

    /// <summary>An auto forward controller's filter or its forwarding throws.</summary>
    public static EventId AutoForwardFailed { get; } = Define(35, nameof(AutoForwardFailed), LogCategories.Error);

    /// <summary>A method of the network processor throws.</summary>
    public static EventId NetworkProcessorFailed { get; } = Define(37, nameof(NetworkProcessorFailed), LogCategories.Error);

    /// <summary>A frame sent through a processor's context fails to route.</summary>
    public static EventId ProcessorSendFailed { get; } = Define(38, nameof(ProcessorSendFailed), LogCategories.Error);

    /// <summary>A reload requested by the user fails because the file cannot be read or parsed.</summary>
    public static EventId NetworkReloadFailed { get; } = Define(39, nameof(NetworkReloadFailed), LogCategories.Error);

    /// <summary>An external system's run loop throws.</summary>
    public static EventId ExternalSystemStoppedUnexpectedly { get; } = Define(40, nameof(ExternalSystemStoppedUnexpectedly), LogCategories.Error);

    /// <summary>A relay or server gets a connection from a user it does not know.</summary>
    public static EventId RejectedConnection { get; } = Define(41, nameof(RejectedConnection), LogCategories.App);

    /// <summary>An IP connection cannot be identified or fails its initial exchange.</summary>
    public static EventId ConnectionDropped { get; } = Define(43, nameof(ConnectionDropped), LogCategories.App);

    /// <summary>A received packet fails reassembly.</summary>
    public static EventId PacketDropped { get; } = Define(44, nameof(PacketDropped), LogCategories.App);

    /// <summary>The IP transport cannot be built, for example because certificates are missing.</summary>
    public static EventId IpConnectionsUnavailable { get; } = Define(45, nameof(IpConnectionsUnavailable), LogCategories.App);

    /// <summary>Packetization is on and its packet size exceeds the HDLC frame limit.</summary>
    public static EventId PacketSizeExceedsHdlc { get; } = Define(46, nameof(PacketSizeExceedsHdlc), LogCategories.App);

    /// <summary>A serial link drops, cannot be opened or reports an error.</summary>
    public static EventId SerialLinkProblem { get; } = Define(47, nameof(SerialLinkProblem), LogCategories.App);

    /// <summary>A node that is not a storage server receives a retrieval request.</summary>
    public static EventId RetrievalIgnored { get; } = Define(50, nameof(RetrievalIgnored), LogCategories.App);

    /// <summary>A send or a relay leaves out recipients whose security level is too low.</summary>
    public static EventId BlockedBySecurityLevel { get; } = Define(51, nameof(BlockedBySecurityLevel), LogCategories.Activity);

    /// <summary>A relay has no connection to its server to forward over.</summary>
    public static EventId CannotForwardServerUnreachable { get; } = Define(53, nameof(CannotForwardServerUnreachable), LogCategories.App);

    /// <summary>A relay or server has no live connection to a recipient.</summary>
    public static EventId CannotDeliverNoConnection { get; } = Define(54, nameof(CannotDeliverNoConnection), LogCategories.App);

    /// <summary>An install on the install screen is refused: the name is not a user of the network, or the certificate is missing, not issued to them or not signed by the authority.</summary>
    public static EventId InstallFailed { get; } = Define(55, nameof(InstallFailed), LogCategories.Activity);

    /// <summary>An operation of an external system throws: connecting, polling, releasing, sending, filtering or processing.</summary>
    public static EventId ExternalSystemFailed { get; } = Define(57, nameof(ExternalSystemFailed), LogCategories.App);

    /// <summary>A frame of the wrong type is sent to an external system.</summary>
    public static EventId ExternalSystemWrongFrameType { get; } = Define(60, nameof(ExternalSystemWrongFrameType), LogCategories.App);

    /// <summary>A message arrives from an external system after it stopped.</summary>
    public static EventId ExternalSystemNotRunning { get; } = Define(62, nameof(ExternalSystemNotRunning), LogCategories.App);

    /// <summary>The host starts.</summary>
    public static EventId AppStarting { get; } = Define(65, nameof(AppStarting), LogCategories.Activity);

    /// <summary>Startup is done, once the networking services are launched or deferred until a user is installed, or once a user is installed on the install screen and the main window opens.</summary>
    public static EventId AppStarted { get; } = Define(66, nameof(AppStarted), LogCategories.Activity);

    /// <summary>The host begins shutting down, so the application is exiting.</summary>
    public static EventId AppExited { get; } = Define(67, nameof(AppExited), LogCategories.Activity);

    /// <summary>A client, relay or server connection to its parent, a child or a sibling server comes up or goes down.</summary>
    public static EventId ConnectionChanged { get; } = Define(68, nameof(ConnectionChanged), LogCategories.Activity);

    /// <summary>The network indicator goes online or offline.</summary>
    public static EventId NetworkStatusChanged { get; } = Define(70, nameof(NetworkStatusChanged), LogCategories.Activity);

    /// <summary>The user chooses Refresh and the network file has been read again.</summary>
    public static EventId NetworkConfigurationReloaded { get; } = Define(72, nameof(NetworkConfigurationReloaded), LogCategories.Activity);

    /// <summary>A reload finds a changed role, certificate store or authority certificate.</summary>
    public static EventId PeersRestarting { get; } = Define(73, nameof(PeersRestarting), LogCategories.App);

    /// <summary>A reload finds a changed interface port or certificate setting.</summary>
    public static EventId InterfaceRestarting { get; } = Define(74, nameof(InterfaceRestarting), LogCategories.App);

    /// <summary>A serial link comes up.</summary>
    public static EventId SerialLinkConnected { get; } = Define(75, nameof(SerialLinkConnected), LogCategories.App);

    /// <summary>A message arrives from a peer.</summary>
    public static EventId MessageReceived { get; } = Define(76, nameof(MessageReceived), LogCategories.Activity);

    /// <summary>A client or server hands a message to the local node.</summary>
    public static EventId MessageDeliveredLocally { get; } = Define(77, nameof(MessageDeliveredLocally), LogCategories.App);

    /// <summary>A read or receive receipt arrives.</summary>
    public static EventId ReceiptReceived { get; } = Define(78, nameof(ReceiptReceived), LogCategories.Activity);

    /// <summary>A message is about to be sent to its recipients.</summary>
    public static EventId MessageSending { get; } = Define(80, nameof(MessageSending), LogCategories.Activity);

    /// <summary>A send to a recipient succeeded.</summary>
    public static EventId MessageDelivered { get; } = Define(81, nameof(MessageDelivered), LogCategories.Activity);

    /// <summary>A send to a recipient failed.</summary>
    public static EventId MessageDeliveryFailed { get; } = Define(82, nameof(MessageDeliveryFailed), LogCategories.Activity);

    /// <summary>The delivery status of a message to a recipient changes.</summary>
    public static EventId DeliveryStatusChanged { get; } = Define(83, nameof(DeliveryStatusChanged), LogCategories.Activity);

    /// <summary>A storage server answers a retrieval request.</summary>
    public static EventId RetrievalAnswered { get; } = Define(84, nameof(RetrievalAnswered), LogCategories.App);

    /// <summary>An external system's connection comes up or goes down.</summary>
    public static EventId ExternalSystemConnectionChanged { get; } = Define(85, nameof(ExternalSystemConnectionChanged), LogCategories.Activity);

    /// <summary>A background service (peer, interface, external systems, network processor, auto forward, disconnect alarm or network indicator) ends other than by being cancelled.</summary>
    public static EventId ServiceStoppedUnexpectedly { get; } = Define(87, nameof(ServiceStoppedUnexpectedly), LogCategories.Crash);

    /// <summary>The initial setup of the main window's view model throws.</summary>
    public static EventId InitializationFailed { get; } = Define(88, nameof(InitializationFailed), LogCategories.Error);

    /// <summary>Reading `User.json` throws.</summary>
    public static EventId LoadUserStateFailed { get; } = Define(89, nameof(LoadUserStateFailed), LogCategories.Error);

    /// <summary>At startup the remembered user or the `--user` name is not in the network file or its certificate fails the check.</summary>
    public static EventId NotInstalled { get; } = Define(90, nameof(NotInstalled), LogCategories.Activity);

    /// <summary>An exception reaches the top-level handler of the application; the activity log says so in general terms.</summary>
    public static EventId UnexpectedError { get; } = Define(93, nameof(UnexpectedError), LogCategories.Activity);

    /// <summary>Networking cannot start or no longer works because the role's configuration or certificates cannot be used; the activity log says so in general terms.</summary>
    public static EventId NetworkingNotWorking { get; } = Define(94, nameof(NetworkingNotWorking), LogCategories.Activity);

    /// <summary>The local interface listener cannot start; the activity log says so in general terms.</summary>
    public static EventId InterfaceNotWorking { get; } = Define(95, nameof(InterfaceNotWorking), LogCategories.Activity);

    /// <summary>A received message is invalid and dropped; the activity log says so without the reason.</summary>
    public static EventId MessageDropped { get; } = Define(96, nameof(MessageDropped), LogCategories.Activity);

    /// <summary>Sending a draft or a staged message fails; the activity log says so without the cause.</summary>
    public static EventId MessageNotSent { get; } = Define(97, nameof(MessageNotSent), LogCategories.Activity);

    /// <summary>The automatic first store of a new draft fails; the activity log says so without the cause.</summary>
    public static EventId DraftNotSaved { get; } = Define(98, nameof(DraftNotSaved), LogCategories.Activity);

    /// <summary>Saving a draft or note when leaving it fails; the activity log says so without the cause.</summary>
    public static EventId EntryNotSaved { get; } = Define(99, nameof(EntryNotSaved), LogCategories.Activity);

    /// <summary>A print job cannot be loaded or printed; the activity log says so without the cause.</summary>
    public static EventId PrintJobFailed { get; } = Define(100, nameof(PrintJobFailed), LogCategories.Activity);

    /// <summary>Storing a message received while the UI runs fails; the activity log says so without the cause.</summary>
    public static EventId ReceivedMessageNotSaved { get; } = Define(101, nameof(ReceivedMessageNotSaved), LogCategories.Activity);

    /// <summary>The initial setup of the main window fails; the activity log says so without the cause.</summary>
    public static EventId StartupIncomplete { get; } = Define(102, nameof(StartupIncomplete), LogCategories.Activity);

    /// <summary>A reload requested by the user fails; the activity log says so without the cause.</summary>
    public static EventId NetworkReloadNotDone { get; } = Define(103, nameof(NetworkReloadNotDone), LogCategories.Activity);

    /// <summary>The remembered user cannot be read at startup; the activity log says so without the cause.</summary>
    public static EventId UserSettingsUnreadable { get; } = Define(104, nameof(UserSettingsUnreadable), LogCategories.Activity);

    /// <summary>A background service ends unexpectedly; the activity log says so in general terms.</summary>
    public static EventId ServiceStopped { get; } = Define(105, nameof(ServiceStopped), LogCategories.Activity);

    /// <summary>An auto forward controller fails on a message; the activity log says so without the cause.</summary>
    public static EventId AutoForwardNotDone { get; } = Define(106, nameof(AutoForwardNotDone), LogCategories.Activity);

    /// <summary>An external system's run loop throws; the activity log says so without the cause.</summary>
    public static EventId ExternalSystemStopped { get; } = Define(107, nameof(ExternalSystemStopped), LogCategories.Activity);

    /// <summary>An external system cannot connect or cannot send a message; the activity log says so without the cause.</summary>
    public static EventId ExternalSystemProblem { get; } = Define(108, nameof(ExternalSystemProblem), LogCategories.Activity);

    /// <summary>A server or relay connection to a parent, a child or a sibling server comes up or goes down.</summary>
    public static EventId PeerConnectionChanged { get; } = Define(109, nameof(PeerConnectionChanged), LogCategories.App);

    /// <summary>A server leaves out recipients whose security level is too low when relaying.</summary>
    public static EventId RelayBlockedBySecurityLevel { get; } = Define(110, nameof(RelayBlockedBySecurityLevel), LogCategories.App);

    /// <summary>A frame is handed to the connection, traced as its serialized bytes with the user it goes to.</summary>
    public static EventId FrameSent { get; } = Define(111, nameof(FrameSent), LogCategories.Frames);

    /// <summary>A frame arrives, traced as its serialized bytes with the user it comes from.</summary>
    public static EventId FrameReceived { get; } = Define(112, nameof(FrameReceived), LogCategories.Frames);

    /// <summary>A packet is handed to the connection, traced as its serialized bytes with the user it goes to.</summary>
    public static EventId PacketSent { get; } = Define(113, nameof(PacketSent), LogCategories.Packets);

    /// <summary>A packet arrives, traced as its serialized bytes with the user it comes from.</summary>
    public static EventId PacketReceived { get; } = Define(114, nameof(PacketReceived), LogCategories.Packets);

    /// <summary>Gets every event, in no particular order.</summary>
    public static IReadOnlyList<EventId> All { get; } = [.. typeof(LogEvents).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(property => property.PropertyType == typeof(EventId)).Select(property => (EventId)property.GetValue(null)!)];

    /// <summary>Gets the category an event is logged under, or <see langword="null"/> for an identifier that is not one of the engine's events.</summary>
    /// <param name="eventId">The event.</param>
    public static string? CategoryOf(EventId eventId) => Categories.GetValueOrDefault(eventId.Id);

    private static EventId Define(int id, string name, string category)
    {
        Categories[id] = category;
        return new EventId(id, name);
    }
}
