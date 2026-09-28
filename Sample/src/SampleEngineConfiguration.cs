namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineConfiguration"/>: maps the engine's logical message fields onto <see cref="SampleMessage"/> and
/// the packet fields onto <see cref="SamplePacket"/>, and states every other setting Sample has distinct, non-config-file
/// behavior worth showing. Everything left unstated uses the engine's default, with <c>config.json</c> applied on top
/// automatically (see <c>Docs/Components/Configuration.md</c>):
/// <list type="bullet">
/// <item><description>home text - a product-appropriate home screen welcome text.</description></item>
/// <item><description>window icon - Sample's own envelope icon instead of the operating system's.</description></item>
/// <item><description>user codes and users - three hard-coded test codes and the three built-in user names matching them.</description></item>
/// <item><description>priorities and blocked tags - three priority levels and both blocked-combination kinds.</description></item>
/// <item><description>print count - prints an alert message twice and every other received message once.</description></item>
/// <item><description>deleting - only drafts and notes can be deleted; Inbox, Outbox, and Activity are protected.</description></item>
/// <item><description>config file - enabled, so Sample honors a <c>--config</c> argument, unlike the engine default.</description></item>
/// <item><description>external systems - a single demo <see cref="SampleExternalSystem"/>, showing the external-system conduit pattern.</description></item>
/// <item><description>packetization - enabled with <see cref="SamplePacket"/>, using the default packet size, window and serializer.</description></item>
/// <item><description>address type labels - renames the <see cref="AddressType.External"/> label to <c>OUTSIDE</c>, matching the <c>Kind</c> vocabulary <see cref="SampleRecipient"/> already uses for it.</description></item>
/// </list>
/// Actual alarm sound playback and printer discovery and driving are real platform behavior always provided by the
/// engine itself, not something Sample states here.
/// </summary>
public sealed class SampleEngineConfiguration : IEngineConfiguration
{
    private readonly Dictionary<string, UserInfo> userCodes = new()
    {
        ["CODE1"] = new UserInfo { Name = "TEST1", Code = "CODE1", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE2"] = new UserInfo { Name = "TEST2", Code = "CODE2", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE3"] = new UserInfo { Name = "TEST3", Code = "CODE3", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" }
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
                .Tag(m => m.Category))
            .Packets<SamplePacket>(packet => packet
                .PayloadId(p => p.Group)
                .Index(p => p.Position)
                .Count(p => p.Total)
                .PayloadLength(p => p.FullLength)
                .Data(p => p.Chunk, (p, value) => p.Chunk = value.ToArray()))
            .HomeText("Select a folder and entry to get started, or create a new draft or note.")
            .WindowIcon(new Uri("avares://BlueHeighliner.Comlink.Sample/Assets/envelope.png"))
            .UserCodes(code => userCodes.GetValueOrDefault(code.ToUpperInvariant()))
            .Users("TEST1", "TEST2", "TEST3")
            .Priorities(
                ("Low", 0),
                ("Medium", 1),
                ("High", 2))
            .BlockTag("SPAM", null)
            .BlockTag(null, 2)
            .AddressTypeLabel(AddressType.External, "OUTSIDE")
            .PrintCount<SampleMessage>(message => message.Alert ? 2 : 1)
            .CanDelete(folder => folder is FolderType.Drafts or FolderType.Notes)
            .ConfigFile()
            .ExternalSystem(new SampleExternalSystem());
}
