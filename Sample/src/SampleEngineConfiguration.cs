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
/// <item><description>command-line overrides - allowed, so Sample honors <c>--config</c> and <c>--user</c> (which its scenario scripts pass), unlike the engine default.</description></item>
/// <item><description>packetization - enabled with <see cref="SamplePacket"/>, using the default packet size, window and serializer.</description></item>
/// <item><description>address type labels - renames the <see cref="AddressType.External"/> label to <c>OUTSIDE</c>, matching the <c>Kind</c> vocabulary <see cref="SampleRecipient"/> already uses for it.</description></item>
/// <item><description>security levels - three placeholder levels (<c>PUBLIC</c>, <c>INTERNAL</c>, <c>RESTRICTED</c>), assigned to users in each scenario's network configuration; the <c>Peer</c> scenario's sites run at <c>PUBLIC</c>, the <c>ClientServer</c>/<c>ServerCluster</c> scenarios' clients at <c>INTERNAL</c>, and their servers at <c>RESTRICTED</c>.</description></item>
/// <item><description>connection and message hooks - a newly connected user is welcomed with who else is currently online (<see cref="IEngineHookContext.ConnectedUsers"/>), everyone still online is told when someone disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply (all via <see cref="IEngineHookContext.SendMessage"/>).</description></item>
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
                .SecurityLevel(m => m.Classification))
            .Packets<SamplePacket>(packet => packet
                .PayloadId(p => p.Group)
                .Index(p => p.Position)
                .Count(p => p.Total)
                .PayloadLength(p => p.FullLength)
                .Data(p => p.Chunk, (p, value) => p.Chunk = value.ToArray()))
            .HomeText("Select a folder and entry to get started, or create a new draft or note.")
            .WindowIcon(new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"))
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
            .PrintCount<SampleMessage>(message => message.Alert ? 2 : 1)
            .CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes)
            .CommandLineOverrides(true)
            .MsmtOptions(options => options with { HandshakeTimeout = TimeSpan.FromSeconds(15), ResponseTimeout = TimeSpan.FromSeconds(60) })
            .MicroGateOptions(options => options with { MaxInfoField = 1024, TransmitWindow = 4 })
            .OnUserConnected(context =>
            {
                string userName = context.TargetUser;
                List<string> others = [.. context.ConnectedUsers.Select(u => u.Name).Where(name => !string.Equals(name, userName, StringComparison.OrdinalIgnoreCase))];
                string body = others.Count > 0 ? $"Also online right now: {string.Join(", ", others)}." : "You're the only one online right now.";
                context.SendMessage(new SampleMessage { Title = "Welcome", Text = body, Recipients = [new SampleRecipient { User = userName }] });
            })
            .OnUserDisconnected(context =>
            {
                string userName = context.TargetUser;
                foreach (UserInfo user in context.ConnectedUsers)
                {
                    context.SendMessage(new SampleMessage { Title = "Offline", Text = $"{userName} just went offline.", Recipients = [new SampleRecipient { User = user.Name }] });
                }
            })
            .OnMessageReceived(context =>
            {
                SampleMessage message = (SampleMessage)context.Message;
                if (string.Equals(message.Category, "PING", StringComparison.OrdinalIgnoreCase))
                {
                    context.SendMessage(new SampleMessage { Title = "Re: " + message.Title, Text = "PONG", Recipients = [new SampleRecipient { User = message.Sender }] });
                }
            })
            .ExportFormat(
                "Text",
                async (entry, stream, cancellation) =>
                {
                    string text = entry switch
                    {
                        MessageExportData message => $"{(message.IsOutbound ? "To" : "From")}: {string.Join(", ", message.Addresses.Select(a => a.UserName))}\nSubject: {message.Subject}\n\n{message.Body}\n",
                        DraftExportData draft => $"Subject: {draft.Subject}\n\n{draft.Body}\n",
                        NoteExportData note => $"{note.Body}\n",
                        _ => throw new ArgumentException($"Unsupported entry type '{entry.GetType()}' for the Text export format.", nameof(entry))
                    };
                    await using StreamWriter writer = new(stream, leaveOpen: true);
                    await writer.WriteAsync(text);
                },
                entryTypes: folder => folder is FolderType.Inbox or FolderType.Outbox or FolderType.Drafts or FolderType.Notes)
            .ImportFormat(
                "CSV",
                async (stream, context, cancellation) =>
                {
                    using StreamReader reader = new(stream, leaveOpen: true);
                    string? line;
                    while ((line = await reader.ReadLineAsync(cancellation)) is not null)
                    {
                        string[] parts = line.Split(',', 3);
                        if (parts.Length < 3) { continue; }
                        context.AddStagedSend(new StagedSendData { Subject = parts[0], Body = parts[2], Addresses = [new AddressRequest { UserName = parts[1] }] });
                    }
                },
                stagedSendMode: StagedSendMode.Sequential,
                stagedSendDelay: TimeSpan.FromSeconds(1))
            .AutoForwardController<SampleMessage>(
                "Escalation",
                users: ["PEER1", "PEER2", "CLIENT1", "CLIENT2"],
                filter: message => message.Alert || string.Equals(message.Category, "URGENT", StringComparison.OrdinalIgnoreCase));
}
