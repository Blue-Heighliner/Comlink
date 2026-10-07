namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineConfiguration"/>: fixes the types the configuration is typed by (<see cref="Frame"/>, <see cref="Packet"/>, <see cref="MessagePriority"/> and <see cref="SecurityLevel"/>), so every handler states its priority and security level as those enums, and maps the engine's logical frame fields onto <see cref="Frame"/> (including whether a frame is a message, which is what the user sees and what is stored) and
/// the packet fields onto <see cref="Packet"/>, and states every other setting Sample has distinct, non-network-file
/// behavior worth showing. Everything left unstated uses the engine's default, with the network configuration file applied on top
/// automatically (see <c>Docs/Components/Configuration.md</c>):
/// <list type="bullet">
/// <item><description>message identifiers - the <see cref="MessageHandler"/> numbers every message in sequence, continuing the counter from the identifier the engine kept from the previous run, instead of the default random GUID.</description></item>
/// <item><description>display - a <see cref="DisplayHandler"/> gives Sample's own envelope window icon instead of the operating system's, a product-appropriate home screen welcome text, renames the Inbox, Outbox and Activity folders to <c>Received</c>, <c>Sent</c> and <c>History</c>, and calls the tag, priority and security level concepts <c>Category</c>, <c>Importance</c> and <c>Confidentiality</c> (with their plurals), everywhere the user interface mentions them.</description></item>
/// <item><description>users - none stated here: every user of the network, with their role, ports, connections, security level and node settings, comes from the network configuration file (<c>--config</c>, or <c>Config.json</c> in the working directory), which each of the <c>Scripts/Scenarios/</c> scenarios supplies for its own network, and an install code is just the name of a user in it.</description></item>
/// <item><description>heartbeats - a <see cref="PacketHeartbeatHandler"/> sends an empty packet flagged as a heartbeat over every IP connection to verify it, at the lowest user priority.</description></item>
/// <item><description>priorities and blocked tags - three user priority levels and two system ones, stated in send order (which is not the order of the enum, whose explicit values are what drafts, exports and frames store), and both blocked-combination kinds.</description></item>
/// <item><description>print count - prints an alert message twice and every other received message once.</description></item>
/// <item><description>drafts - a <see cref="DraftHandler"/> shows a draft 60 monospace characters wide by default (the user may change it between 10 and 90; it only changes how the draft is shown, never its text), and gives each kind of message its own header: <c>ALERT - ACTION REQUIRED</c> for an alert, <c>URGENT - PLEASE REPLY</c> or <c>REPORT - FOR YOUR REVIEW</c> for those tags, plus <c>RESTRICTED - DO NOT FORWARD</c> at the restricted security level, and no header at all for any other message (a plain one, or one tagged <c>NOTICE</c>).</description></item>
/// <item><description>deleting - a <see cref="DeleteHandler"/> lets users delete only drafts and notes can be deleted; Inbox, Outbox, and Activity are protected.</description></item>
/// <item><description>connection identification - a <see cref="IdentityProcessor"/> carries out an initial packet exchange on every connection: the node that opens it sends a <see cref="Packet"/> whose chunk is its user name, the accepting node answers with one carrying its own, and each marks the connection connected as the user the other named, instead of by the peer's certificate or (for a serial cable) its port. Like all traffic between nodes they are serialized instances of the packet type, nothing else.</description></item>
/// <item><description>command-line overrides - allowed, so Sample honors <c>--config</c> and <c>--user</c> (which its scenario scripts pass), unlike the engine default.</description></item>
/// <item><description>packetization - enabled with <see cref="Packet"/>, with a packet size of 1024 bytes (what one HDLC frame carries, as set by the HDLC options below) and the default window and serializer.</description></item>
/// <item><description>address type labels - renames the <see cref="AddressType.External"/> label to <c>OUTSIDE</c>, matching the <c>Kind</c> vocabulary <see cref="Recipient"/> already uses for it.</description></item>
/// <item><description>security levels - three placeholder levels (<c>PUBLIC</c>, <c>INTERNAL</c>, <c>RESTRICTED</c>), assigned to users in each scenario's network configuration; the <c>ClientServer</c>/<c>ServerCluster</c> scenarios' clients at <c>INTERNAL</c>, and their servers at <c>RESTRICTED</c>.</description></item>
/// <item><description>custom frame serialization - <see cref="JsonSerializer"/> sends every <see cref="Frame"/> across the network as JSON instead of the default protobuf-net.</description></item>
/// <item><description>a <see cref="NetworkProcessor"/> reacting to peer activity - any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply (via <see cref="INetworkContext{TFrame}.Send"/>), except on a server or relay, which compose nothing.</description></item>
/// <item><description>export formats - a plain-text alternative to the built-in JSON export, restricted to messages, drafts, and notes (an activity log's structured entries don't read naturally as prose).</description></item>
/// <item><description>import formats - a CSV reader that stages one send per <c>User,Body</c> line for the user to review and send from the staged send screen, one at a time a second apart.</description></item>
/// <item><description>server storage - every server keeps a copy of every message one of its own children sends and answers a client's RETRIEVE request, which names the server the message is stored on.</description></item>
/// <item><description>auto forward controllers - an "Escalation" controller, open to every Client scenario site, that forwards any received alert or <c>URGENT</c>-tagged message to whichever users its target list names.</description></item>
/// </list>
/// Actual alarm sound playback and printer discovery and driving are real platform behavior always provided by the
/// engine itself, not something Sample states here.
/// </summary>
public sealed class EngineConfiguration : IEngineConfiguration
{
    /// <summary>Application entry point; starts the engine with this configuration.</summary>
    [STAThread]
    public static async Task Main(string[] args)
        => await Engine.Start<EngineConfiguration>(args);

    /// <inheritdoc />
    public void Configure(IEngineBuilder engine)
        => engine.Types<Frame, Packet, MessagePriority, SecurityLevel>()
            .Display<DisplayHandler>()
            .Deletes<DeleteHandler>()
            .Drafts<DraftHandler>()
            .Prints<PrintHandler>()
            .Logs<LogHandler>()
            .CommandLineOverrides(true)
            .Frames()
                .Serializer<JsonSerializer>()
                .Processor<NetworkProcessor>()
                .Message<MessageHandler>()
                .AutoForward<EscalationController>()
                .Retrieval<RetrievalHandler>()
                .ReadReceipt<ReadReceiptHandler>()
                .ReceiveReceipt<ReceiveReceiptHandler>()
            .Packets()
                .InitialProcessor<IdentityProcessor>()
                .Frame<FramePacketHandler>()
                .Heartbeat<PacketHeartbeatHandler>()
                .Size(1024)
            .Priorities()
                .Priority(MessagePriority.Low)
                .Priority(MessagePriority.Medium)
                .Priority(MessagePriority.Retrieval).Mode(PriorityMode.System)
                .Priority(MessagePriority.High)
                .Priority(MessagePriority.Receipt).Mode(PriorityMode.System)
                .Block(null, "SPAM")
                .Block(MessagePriority.High, null)
            .SecurityLevels()
                .Level(SecurityLevel.Public).Color("#2E7D32")
                .Level(SecurityLevel.Internal).Color("#1565C0")
                .Level(SecurityLevel.Restricted).Color("#C62828")
            .AddressTypes()
                .Type(AddressType.External).Label("OUTSIDE")
            .Connections()
                .Msmt(new MsmtConnectionOptions { HandshakeTimeout = TimeSpan.FromSeconds(15), ResponseTimeout = TimeSpan.FromSeconds(60) })
                .Hdlc(new HdlcPeerOptions { MaxInfoField = 1024, TransmitWindow = 4 })
            .Exports()
                .Format<TextExportFormat>()
            .Imports()
                .Format<CsvImportFormat>();
}
