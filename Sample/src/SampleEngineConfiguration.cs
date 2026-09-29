namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineConfiguration"/>: maps the engine's logical message fields onto <see cref="SampleMessage"/> and
/// the packet fields onto <see cref="SamplePacket"/>, and states every other setting Sample has distinct, non-config-file
/// behavior worth showing. Everything left unstated uses the engine's default, with <c>config.json</c> applied on top
/// automatically (see <c>Docs/Components/Configuration.md</c>):
/// <list type="bullet">
/// <item><description>home text - a product-appropriate home screen welcome text.</description></item>
/// <item><description>window icon - Sample's own envelope icon instead of the operating system's.</description></item>
/// <item><description>user codes and users - one hard-coded install code per site used across <c>Scripts/Scenarios/</c> (Client1, Client2, Server, Server1, Server2, Peer1, Peer2), so every scenario's <c>UserName</c> is a recognized user without needing its own code.</description></item>
/// <item><description>priorities and blocked tags - three priority levels and both blocked-combination kinds.</description></item>
/// <item><description>print count - prints an alert message twice and every other received message once.</description></item>
/// <item><description>deleting - only drafts and notes can be deleted; Inbox, Outbox, and Activity are protected.</description></item>
/// <item><description>config file - enabled, so Sample honors a <c>--config</c> argument, unlike the engine default.</description></item>
/// <item><description>external systems - a single demo <see cref="SampleExternalSystem"/>, showing the external-system conduit pattern.</description></item>
/// <item><description>packetization - enabled with <see cref="SamplePacket"/>, using the default packet size, window and serializer.</description></item>
/// <item><description>address type labels - renames the <see cref="AddressType.External"/> label to <c>OUTSIDE</c>, matching the <c>Kind</c> vocabulary <see cref="SampleRecipient"/> already uses for it.</description></item>
/// <item><description>security levels - three placeholder levels (<c>PUBLIC</c>, <c>INTERNAL</c>, <c>RESTRICTED</c>); the <c>Peer</c> scenario's sites run at <c>PUBLIC</c>, the <c>ClientServer</c>/<c>ServerCluster</c> scenarios' clients at <c>INTERNAL</c>, and their servers at <c>RESTRICTED</c>.</description></item>
/// <item><description>connection and message hooks - a newly connected user is welcomed with who else is currently online (<see cref="IEngineHookContext.ConnectedUsers"/>), everyone still online is told when someone disconnects, and any received message tagged <c>PING</c> gets an automatic <c>PONG</c> reply (all via <see cref="IEngineHookContext.SendMessage"/>).</description></item>
/// <item><description>export formats - a plain-text alternative to the built-in JSON export, restricted to messages, drafts, and notes (an activity log's structured entries don't read naturally as prose).</description></item>
/// </list>
/// Actual alarm sound playback and printer discovery and driving are real platform behavior always provided by the
/// engine itself, not something Sample states here.
/// </summary>
public sealed class SampleEngineConfiguration : IEngineConfiguration
{
    private readonly Dictionary<string, UserInfo> userCodes = new()
    {
        ["CLIENT1"] = new UserInfo { Name = "Client1", Code = "CLIENT1" },
        ["CLIENT2"] = new UserInfo { Name = "Client2", Code = "CLIENT2" },
        ["SERVER"] = new UserInfo { Name = "Server", Code = "SERVER" },
        ["SERVER1"] = new UserInfo { Name = "Server1", Code = "SERVER1" },
        ["SERVER2"] = new UserInfo { Name = "Server2", Code = "SERVER2" },
        ["PEER1"] = new UserInfo { Name = "Peer1", Code = "PEER1" },
        ["PEER2"] = new UserInfo { Name = "Peer2", Code = "PEER2" }
    };

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
            .UserCodes(code => userCodes.GetValueOrDefault(code.ToUpperInvariant()))
            .Users("Client1", "Client2", "Server", "Server1", "Server2", "Peer1", "Peer2")
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
            .UserSecurityLevel(userName => userName.ToUpperInvariant() switch
            {
                "SERVER" or "SERVER1" or "SERVER2" => "RESTRICTED",
                "CLIENT1" or "CLIENT2" => "INTERNAL",
                _ => "PUBLIC"
            })
            .PrintCount<SampleMessage>(message => message.Alert ? 2 : 1)
            .CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes)
            .ConfigFile()
            .ExternalSystem(new SampleExternalSystem())
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
                entryTypes: folder => folder is FolderType.Inbox or FolderType.Outbox or FolderType.Drafts or FolderType.Notes);
}
