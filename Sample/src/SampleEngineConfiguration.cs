namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineConfiguration"/>: maps the engine's logical message fields onto <see cref="SampleMessage"/> and
/// the packet fields onto <see cref="SamplePacket"/>, and states every other setting Sample has distinct, non-network-file
/// behavior worth showing. Everything left unstated uses the engine's default, with the network configuration file applied on top
/// automatically (see <c>Docs/Components/Configuration.md</c>):
/// <list type="bullet">
/// <item><description>home text - a product-appropriate home screen welcome text.</description></item>
/// <item><description>window icon - Sample's own envelope icon instead of the operating system's.</description></item>
/// <item><description>users - none stated here: every user of the network, with their role, ports, connections, security level and node settings, comes from the network configuration file (<c>--config</c>, or <c>Config.json</c> in the working directory), which each of the <c>Scripts/Scenarios/</c> scenarios supplies for its own network, and an install code is just the name of a user in it.</description></item>
/// <item><description>priorities and blocked tags - three priority levels and both blocked-combination kinds.</description></item>
/// <item><description>print count - prints an alert message twice and every other received message once.</description></item>
/// <item><description>deleting - only drafts and notes can be deleted; Inbox, Outbox, and Activity are protected.</description></item>
/// <item><description>connection identification - a <see cref="SampleIdentityProcessor"/> carries out an initial packet exchange on every connection: the node that opens it sends a <see cref="SamplePacket"/> whose chunk is its user name, the accepting node answers with one carrying its own, and each marks the connection connected as the user the other named, instead of by the peer's certificate or (for a serial cable) its port. Like all traffic between nodes they are serialized instances of the packet type, nothing else.</description></item>
/// <item><description>command-line overrides - allowed, so Sample honors <c>--config</c> and <c>--user</c> (which its scenario scripts pass), unlike the engine default.</description></item>
/// <item><description>packetization - enabled with <see cref="SamplePacket"/>, using the default packet size, window and serializer.</description></item>
/// <item><description>address type labels - renames the <see cref="AddressType.External"/> label to <c>OUTSIDE</c>, matching the <c>Kind</c> vocabulary <see cref="SampleRecipient"/> already uses for it.</description></item>
/// <item><description>security levels - three placeholder levels (<c>PUBLIC</c>, <c>INTERNAL</c>, <c>RESTRICTED</c>), assigned to users in each scenario's network configuration; the <c>Peer</c> scenario's sites run at <c>PUBLIC</c>, the <c>ClientServer</c>/<c>ServerCluster</c> scenarios' clients at <c>INTERNAL</c>, and their servers at <c>RESTRICTED</c>.</description></item>
/// <item><description>custom message serialization - <see cref="SampleJsonSerializer"/> sends every <see cref="SampleMessage"/> across the network as JSON instead of the default protobuf-net.</description></item>
/// <item><description>a <see cref="SampleNetworkProcessor"/> reacting to peer activity - a newly connected user is welcomed with who else is currently online (<see cref="IEngineContext.ConnectedUsers"/>), everyone still online is told when someone disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply (all via <see cref="INetworkContext{TMessage}.Send"/>).</description></item>
/// <item><description>export formats - a plain-text alternative to the built-in JSON export, restricted to messages, drafts, and notes (an activity log's structured entries don't read naturally as prose).</description></item>
/// <item><description>import formats - a CSV reader that stages one send per <c>Subject,User,Body</c> line for the user to review and send from the staged send screen, one at a time a second apart.</description></item>
/// <item><description>server storage - <c>Server</c> (ClientServer scenario) and <c>Server1</c> (ServerCluster scenario) (<c>StoresMessages</c> in the network configuration) keep a copy of every message they route and answer a client's RETRIEVE request; <c>Server2</c> does not.</description></item>
/// <item><description>auto forward controllers - an "Escalation" controller, open to every Peer/Client scenario site, that forwards any received alert or <c>URGENT</c>-tagged message to whichever users its target list names.</description></item>
/// </list>
/// Actual alarm sound playback and printer discovery and driving are real platform behavior always provided by the
/// engine itself, not something Sample states here.
/// </summary>
public sealed class SampleEngineConfiguration : IEngineConfiguration
{
    /// <inheritdoc />
    public IEngineBuilder Configure(IEngineBuilder engine)
        => engine
            .Message<SampleMessage>(message => message
                .Serializer<SampleJsonSerializer>()
                .Processor<SampleNetworkProcessor>()
                .Id(m => m.Id)
                .Sender(m => m.Sender)
                .Subject(m => m.Title)
                .Body(m => m.Text)
                .Addresses(
                    m => m.Recipients.Select(r => (r.User, r.Kind switch { "CC" => AddressType.Cc, "OUTSIDE" => AddressType.External, _ => AddressType.To }, r.Note)),
                    (m, value) => m.Recipients = [.. value.Select(a => new SampleRecipient { User = a.Name, Kind = a.Type switch { AddressType.Cc => "CC", AddressType.External => "OUTSIDE", _ => "TO" }, Note = a.Information })])
                .SentAt(m => m.Timestamp)
                .ConfirmationId(m => m.ConfirmsId)
                .Retrieval(r => r.IsRequest(m => m.IsRetrieval).From(m => m.RetrievalFrom).To(m => m.RetrievalTo).Authors(m => m.RetrievalAuthors, (m, v) => m.RetrievalAuthors = [.. v]).Destinations(m => m.RetrievalDestinations, (m, v) => m.RetrievalDestinations = [.. v]).Ids(m => m.RetrievalIds, (m, v) => m.RetrievalIds = [.. v]))
                .IsAlert(m => m.Alert)
                .Priority(m => m.Importance)
                .Tag(m => m.Category)
                .SecurityLevel(m => m.Classification)
                .PrintCount(m => m.Alert ? 2 : 1)
                .AutoForward(
                    "Escalation",
                    ["PEER1", "PEER2", "CLIENT1", "CLIENT2"],
                    message => message.Alert || string.Equals(message.Category, "URGENT", StringComparison.OrdinalIgnoreCase)))
            .Packets<SamplePacket>(packet => packet
                .InitialProcessor<SampleIdentityProcessor>()
                .PayloadId(p => p.Group)
                .Index(p => p.Position)
                .Count(p => p.Total)
                .PayloadLength(p => p.FullLength)
                .Data(p => p.Chunk, (p, value) => p.Chunk = value.ToArray()))
            .HomeText("Select a folder and entry to get started, or create a new draft or note.")
            .WindowIcon("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png")
            .Priorities(
                ("Low", 0),
                ("Medium", 1),
                ("High", 2))
            .BlockTag("SPAM", null)
            .BlockTag(null, 2)
            .AddressTypeLabel(AddressType.External, "OUTSIDE")
            .SecurityLevels(
                ("PUBLIC", "#2E7D32"),
                ("INTERNAL", "#1565C0"),
                ("RESTRICTED", "#C62828"))
            .CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes)
            .CommandLineOverrides(true)
            .MsmtOptions(options => options with { HandshakeTimeout = TimeSpan.FromSeconds(15), ResponseTimeout = TimeSpan.FromSeconds(60) })
            .MicroGateOptions(options => options with { MaxInfoField = 1024, TransmitWindow = 4 })
            .ExportFormat<SampleTextExportFormat>()
            .ImportFormat<SampleCsvImportFormat>();
}
