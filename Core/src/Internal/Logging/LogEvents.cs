namespace BlueHeighliner.Comlink;

/// <summary>
/// Every event the engine logs, each with an identifier that is unique and never reused: <c>CL</c> and its number, such as <c>CL56</c>, carried as the <see cref="EventId.Name"/> of the event, which is how an
/// identifier travels through the loggers (the host's own are whatever text it gave). The identifier is written in the log output as a bracketed element
/// and is listed with each entry of the activity log; <c>Docs/Components/Logging.md</c> describes every event.
/// </summary>
internal static class LogEvents
{
    public static string Prefix { get; } = "CL";

    private static Dictionary<int, string> Categories { get; } = [];

    /// <summary>An exception reaches the top-level handler of the application.</summary>
    public static EventId UnhandledException { get; } = Define(1, LogCategories.Crash);

    /// <summary>The role's peer service cannot be built, for example because a certificate file is missing.</summary>
    public static EventId NetworkingCouldNotStart { get; } = Define(2, LogCategories.Error);

    /// <summary>The network configuration file is invalid for the role: a client has no parent (at start or after a reload), or a server is missing from its own server map.</summary>
    public static EventId InvalidConfigurationFile { get; } = Define(3, LogCategories.Error);

    /// <summary>The local interface listener cannot be built, for example because a certificate file is missing.</summary>
    public static EventId InterfaceCannotStart { get; } = Define(8, LogCategories.Error);

    /// <summary>A payload is too large for the connection it is sent over, whether MSMT, the packetizer or an HDLC frame.</summary>
    public static EventId PayloadTooLarge { get; } = Define(11, LogCategories.Error);

    /// <summary>A handler of a user connecting or disconnecting throws.</summary>
    public static EventId ConnectionEventHandlerFailed { get; } = Define(22, LogCategories.Error);

    /// <summary>Storing a message received while the UI runs throws.</summary>
    public static EventId StoreReceivedMessageFailed { get; } = Define(23, LogCategories.Error);

    /// <summary>A storage server fails to save a message it relayed.</summary>
    public static EventId StoreMessageCopyFailed { get; } = Define(24, LogCategories.Error);

    /// <summary>A storage server fails to read messages for a retrieval request.</summary>
    public static EventId ReadStoredMessagesFailed { get; } = Define(25, LogCategories.Error);

    /// <summary>Sending a draft or a staged message throws.</summary>
    public static EventId SendFailed { get; } = Define(26, LogCategories.Error);

    /// <summary>The automatic first store of a new draft throws.</summary>
    public static EventId StoreNewDraftFailed { get; } = Define(28, LogCategories.Error);

    /// <summary>Selecting the folder and entry of what was opened throws.</summary>
    public static EventId RevealOpenedEntryFailed { get; } = Define(29, LogCategories.Error);

    /// <summary>Saving a draft or note when leaving it throws.</summary>
    public static EventId SaveOnLeavingFailed { get; } = Define(30, LogCategories.Error);

    /// <summary>An import finds a message or draft with a priority that is not configured.</summary>
    public static EventId ImportPriorityRejected { get; } = Define(31, LogCategories.Activity);

    /// <summary>Reading the content of a print job throws.</summary>
    public static EventId PrintContentLoadFailed { get; } = Define(33, LogCategories.Error);

    /// <summary>Sending a print job to the printer throws.</summary>
    public static EventId PrintFailed { get; } = Define(34, LogCategories.Error);

    /// <summary>A method of the frame handler throws.</summary>
    public static EventId FrameHandlerFailed { get; } = Define(37, LogCategories.Error);

    /// <summary>A reload requested by the user fails because the file cannot be read or parsed.</summary>
    public static EventId NetworkReloadFailed { get; } = Define(39, LogCategories.Error);

    /// <summary>An external system's run loop throws.</summary>
    public static EventId ExternalSystemStoppedUnexpectedly { get; } = Define(40, LogCategories.Error);

    /// <summary>A server gets a connection from a user it does not know.</summary>
    public static EventId RejectedConnection { get; } = Define(41, LogCategories.App);

    /// <summary>An IP connection cannot be identified or fails its handshake.</summary>
    public static EventId ConnectionDropped { get; } = Define(43, LogCategories.App);

    /// <summary>A received packet fails reassembly.</summary>
    public static EventId PacketDropped { get; } = Define(44, LogCategories.App);

    /// <summary>The IP transport cannot be built, for example because certificates are missing.</summary>
    public static EventId IpConnectionsUnavailable { get; } = Define(45, LogCategories.App);

    /// <summary>Packetization is on and its payload size leaves no room for a packet's own fields within the HDLC frame limit.</summary>
    public static EventId MaxPayloadSizeExceedsHdlc { get; } = Define(46, LogCategories.App);

    /// <summary>A server has no live connection to a recipient.</summary>
    public static EventId CannotDeliverNoConnection { get; } = Define(54, LogCategories.App);

    /// <summary>An operation of an external system throws: connecting, polling, releasing, sending, filtering or processing.</summary>
    public static EventId ExternalSystemFailed { get; } = Define(57, LogCategories.App);

    /// <summary>A frame of the wrong type is sent to an external system.</summary>
    public static EventId ExternalSystemWrongFrameType { get; } = Define(60, LogCategories.App);

    /// <summary>A message arrives from an external system after it stopped.</summary>
    public static EventId ExternalSystemNotRunning { get; } = Define(62, LogCategories.App);

    /// <summary>The host starts.</summary>
    public static EventId AppStarting { get; } = Define(65, LogCategories.Activity);

    /// <summary>Startup is done, once the networking services are launched.</summary>
    public static EventId AppStarted { get; } = Define(66, LogCategories.Activity);

    /// <summary>The host begins shutting down, so the application is exiting.</summary>
    public static EventId AppExited { get; } = Define(67, LogCategories.Activity);

    /// <summary>A client or server connection to its parent, a child or a sibling server comes up or goes down.</summary>
    public static EventId ConnectionChanged { get; } = Define(68, LogCategories.Activity);

    /// <summary>The network indicator goes online or offline.</summary>
    public static EventId NetworkStatusChanged { get; } = Define(70, LogCategories.Activity);

    /// <summary>The user chooses Refresh and the network file has been read again.</summary>
    public static EventId NetworkConfigurationReloaded { get; } = Define(72, LogCategories.Activity);

    /// <summary>A reload finds a changed role, certificate store or authority certificate.</summary>
    public static EventId PeersRestarting { get; } = Define(73, LogCategories.App);

    /// <summary>A reload finds a changed interface port or certificate setting.</summary>
    public static EventId InterfaceRestarting { get; } = Define(74, LogCategories.App);

    /// <summary>A message arrives from a peer.</summary>
    public static EventId MessageReceived { get; } = Define(76, LogCategories.Activity);

    /// <summary>A message is about to be sent to its recipients.</summary>
    public static EventId MessageSending { get; } = Define(80, LogCategories.Activity);

    /// <summary>The delivery status of a message to a recipient changes.</summary>
    public static EventId DeliveryStatusChanged { get; } = Define(83, LogCategories.Activity);

    /// <summary>A storage server answers a retrieval request.</summary>
    public static EventId RetrievalAnswered { get; } = Define(84, LogCategories.App);

    /// <summary>An external system's connection comes up or goes down.</summary>
    public static EventId ExternalSystemConnectionChanged { get; } = Define(85, LogCategories.Activity);

    /// <summary>A background service (peer, interface, external systems, frame handler, auto forward, disconnect alarm or network indicator) ends other than by being cancelled.</summary>
    public static EventId ServiceStoppedUnexpectedly { get; } = Define(87, LogCategories.Crash);

    /// <summary>The initial setup of the main window's view model throws.</summary>
    public static EventId InitializationFailed { get; } = Define(88, LogCategories.Error);

    /// <summary>An exception reaches the top-level handler of the application; the activity log says so in general terms.</summary>
    public static EventId UnexpectedError { get; } = Define(93, LogCategories.Activity);

    /// <summary>Networking cannot start or no longer works because the role's configuration or certificates cannot be used; the activity log says so in general terms.</summary>
    public static EventId NetworkingNotWorking { get; } = Define(94, LogCategories.Activity);

    /// <summary>The local interface listener cannot start; the activity log says so in general terms.</summary>
    public static EventId InterfaceNotWorking { get; } = Define(95, LogCategories.Activity);

    /// <summary>Sending a draft or a staged message fails; the activity log says so without the cause.</summary>
    public static EventId MessageNotSent { get; } = Define(97, LogCategories.Activity);

    /// <summary>The automatic first store of a new draft fails; the activity log says so without the cause.</summary>
    public static EventId DraftNotSaved { get; } = Define(98, LogCategories.Activity);

    /// <summary>Saving a draft or note when leaving it fails; the activity log says so without the cause.</summary>
    public static EventId EntryNotSaved { get; } = Define(99, LogCategories.Activity);

    /// <summary>A print job cannot be loaded or printed; the activity log says so without the cause.</summary>
    public static EventId PrintJobFailed { get; } = Define(100, LogCategories.Activity);

    /// <summary>Storing a message received while the UI runs fails; the activity log says so without the cause.</summary>
    public static EventId ReceivedMessageNotSaved { get; } = Define(101, LogCategories.Activity);

    /// <summary>The initial setup of the main window fails; the activity log says so without the cause.</summary>
    public static EventId StartupIncomplete { get; } = Define(102, LogCategories.Activity);

    /// <summary>A reload requested by the user fails; the activity log says so without the cause.</summary>
    public static EventId NetworkReloadNotDone { get; } = Define(103, LogCategories.Activity);

    /// <summary>A background service ends unexpectedly; the activity log says so in general terms.</summary>
    public static EventId ServiceStopped { get; } = Define(105, LogCategories.Activity);

    /// <summary>An external system's run loop throws; the activity log says so without the cause.</summary>
    public static EventId ExternalSystemStopped { get; } = Define(107, LogCategories.Activity);

    /// <summary>An external system cannot connect or cannot send a message; the activity log says so without the cause.</summary>
    public static EventId ExternalSystemProblem { get; } = Define(108, LogCategories.Activity);

    /// <summary>A server connection to a parent, a child or a sibling server comes up or goes down.</summary>
    public static EventId PeerConnectionChanged { get; } = Define(109, LogCategories.App);

    /// <summary>A frame is handed to the connection, traced as its serialized bytes with the user it goes to.</summary>
    public static EventId FrameSent { get; } = Define(111, LogCategories.Frames);

    /// <summary>A frame arrives, traced as its serialized bytes with the user it comes from.</summary>
    public static EventId FrameReceived { get; } = Define(112, LogCategories.Frames);

    /// <summary>A packet is handed to the connection, traced as its serialized bytes with the user it goes to.</summary>
    public static EventId PacketSent { get; } = Define(113, LogCategories.Packets);

    /// <summary>A packet arrives, traced as its serialized bytes with the user it comes from.</summary>
    public static EventId PacketReceived { get; } = Define(114, LogCategories.Packets);

    /// <summary>Avalonia reports a warning or an error about itself, such as a binding that could not be resolved.</summary>
    public static EventId AvaloniaReported { get; } = Define(116, LogCategories.Avalonia);

    /// <summary>A refresh finds that the node's certificate now names another user, which becomes the current user.</summary>
    public static EventId UserChanged { get; } = Define(117, LogCategories.Activity);

    /// <summary>The node cannot be identified, at startup or after a refresh: a certificate file is missing or unreadable, the user certificate is not signed by the authority certificate, or it is not issued to a user of the network. The UI shows the problem in place of the app.</summary>
    public static EventId IdentityFailed { get; } = Define(118, LogCategories.Activity);

    /// <summary>The host's configuration handler throws.</summary>
    public static EventId ConfigurationHandlerFailed { get; } = Define(119, LogCategories.Error);

    /// <summary>The host's configuration handler throws; the activity log says so in general terms.</summary>
    public static EventId ConfigurationHandlerStopped { get; } = Define(120, LogCategories.Activity);

    /// <summary>The IP listener starts accepting connections.</summary>
    public static EventId IpListening { get; } = Define(121, LogCategories.Ip);

    /// <summary>The IP listener stops accepting connections.</summary>
    public static EventId IpListenerStopped { get; } = Define(122, LogCategories.Ip);

    /// <summary>An outgoing IP connection is being opened; repeated attempts to the same point are not logged again until it has connected.</summary>
    public static EventId IpConnecting { get; } = Define(123, LogCategories.Ip);

    /// <summary>An outgoing IP connection could not be opened; not logged again for the same point until it has connected.</summary>
    public static EventId IpConnectFailed { get; } = Define(124, LogCategories.Ip);

    /// <summary>An outgoing IP connection is established.</summary>
    public static EventId IpConnected { get; } = Define(125, LogCategories.Ip);

    /// <summary>The listener accepts an incoming IP connection.</summary>
    public static EventId IpAccepted { get; } = Define(126, LogCategories.Ip);

    /// <summary>An established IP connection closes.</summary>
    public static EventId IpDisconnected { get; } = Define(127, LogCategories.Ip);

    /// <summary>An incoming IP connection ends without being established, such as one that does not complete its handshake.</summary>
    public static EventId IpRefused { get; } = Define(128, LogCategories.Ip);

    /// <summary>A serial port moves to another state (ready, connecting, connected or disconnected); the states of retries that follow a failed attempt are not logged until the link has connected again.</summary>
    public static EventId SerialPortState { get; } = Define(129, LogCategories.Serial);

    /// <summary>A serial port cannot be opened or its link cannot be formed; not logged again until the link has connected.</summary>
    public static EventId SerialPortFailed { get; } = Define(130, LogCategories.Serial);

    /// <summary>A serial link is closed or opened again by the application.</summary>
    public static EventId SerialLinkSwitched { get; } = Define(131, LogCategories.Serial);

    /// <summary>The modem wires of a serial port change.</summary>
    public static EventId SerialSignals { get; } = Define(132, LogCategories.Serial);

    /// <summary>A serial port reports an error that does not break the link.</summary>
    public static EventId SerialPortError { get; } = Define(133, LogCategories.Serial);

    /// <summary>Gets every event, in no particular order.</summary>
    public static IReadOnlyList<EventId> All { get; } = [.. typeof(LogEvents).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(property => property.PropertyType == typeof(EventId)).Select(property => (EventId)property.GetValue(null)!)];

    /// <summary>Gets the category an event is logged under, or <see langword="null"/> for an identifier that is not one of the engine's events, which a log of the host's own or a library's can look like by its number alone.</summary>
    /// <param name="eventId">The event.</param>
    public static string? CategoryOf(EventId eventId) => eventId.Id > 0 && eventId.Name == $"{Prefix}{eventId.Id}" ? Categories.GetValueOrDefault(eventId.Id) : null;

    private static EventId Define(int id, string category)
    {
        Categories[id] = category;
        return new EventId(id, $"{Prefix}{id}");
    }
}
