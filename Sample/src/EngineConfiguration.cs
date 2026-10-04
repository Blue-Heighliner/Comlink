namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineConfiguration"/>: maps the engine's logical frame fields onto <see cref="Frame"/> (including whether a frame is a message, which is what the user sees and what is stored) and
/// the packet fields onto <see cref="Packet"/>, and states every other setting Sample has distinct, non-network-file
/// behavior worth showing. Everything left unstated uses the engine's default, with the network configuration file applied on top
/// automatically (see <c>Docs/Components/Configuration.md</c>):
/// <list type="bullet">
/// <item><description>message identifiers - the <see cref="MessageHandler"/> numbers every message in sequence, continuing the counter from the identifier the engine kept from the previous run, instead of the default random GUID.</description></item>
/// <item><description>home text - a product-appropriate home screen welcome text.</description></item>
/// <item><description>window icon - Sample's own envelope icon instead of the operating system's.</description></item>
/// <item><description>users - none stated here: every user of the network, with their role, ports, connections, security level and node settings, comes from the network configuration file (<c>--config</c>, or <c>Config.json</c> in the working directory), which each of the <c>Scripts/Scenarios/</c> scenarios supplies for its own network, and an install code is just the name of a user in it.</description></item>
/// <item><description>heartbeats - a <see cref="PacketHeartbeatHandler"/> sends an empty packet flagged as a heartbeat over every IP connection to verify it, at the lowest user priority.</description></item>
/// <item><description>priorities and blocked tags - three user priority levels, two system ones and both blocked-combination kinds.</description></item>
/// <item><description>print count - prints an alert message twice and every other received message once.</description></item>
/// <item><description>deleting - only drafts and notes can be deleted; Inbox, Outbox, and Activity are protected.</description></item>
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
    public IEngineBuilder Configure(IEngineBuilder engine)
        => engine
            .Frames<Frame>(frame => frame
                .Serializer<JsonSerializer>()
                .Processor<NetworkProcessor>()
                .Message<MessageHandler>()
                .AutoForward<EscalationController>()
                .Retrieval<RetrievalHandler>()
                .ReadReceipt<ReadReceiptHandler>()
                .ReceiveReceipt<ReceiveReceiptHandler>())
            .Packets<Packet>(packet => packet
                .InitialProcessor<IdentityProcessor>()
                .Frame<FramePacketHandler>()
                .Heartbeat<PacketHeartbeatHandler>())
            .HomeText("Select a folder and entry to get started, or create a new draft or note.")
            .WindowIcon("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png")
            .Priorities<MessagePriority>(priorities => priorities
                .Priority(MessagePriority.Retrieval).Mode(PriorityMode.System)
                .Priority(MessagePriority.Receipt).Mode(PriorityMode.System)
                .Block("SPAM", null)
                .Block(null, MessagePriority.High))
            .AddressTypeLabel(AddressType.External, "OUTSIDE")
            .SecurityLevels<SecurityLevel>(levels => levels
                .Level(SecurityLevel.Public).Color("#2E7D32")
                .Level(SecurityLevel.Internal).Color("#1565C0")
                .Level(SecurityLevel.Restricted).Color("#C62828"))
            .PacketSize(1024)
            .CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes)
            .CommandLineOverrides(true)
            .MsmtOptions(new MsmtConnectionOptions { HandshakeTimeout = TimeSpan.FromSeconds(15), ResponseTimeout = TimeSpan.FromSeconds(60) })
            .HdlcOptions(new HdlcPeerOptions { MaxInfoField = 1024, TransmitWindow = 4 })
            .ExportFormat<TextExportFormat>()
            .ImportFormat<CsvImportFormat>();
}
