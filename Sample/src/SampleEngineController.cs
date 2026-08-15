namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IEngineController"/> mapping the engine's logical message fields onto
/// <see cref="SampleMessage"/> (with no casting required — see <see cref="DefaultEngineController{TMessage}"/>),
/// and demonstrating every other control override Sample has distinct, non-config-file behavior worth
/// showing — every other member uses the Engine default, with <c>config.json</c> applied on top
/// automatically (see <c>Docs/Control.md</c>):
/// <list type="bullet">
/// <item><description><see cref="HomeText"/> — a product-appropriate home screen welcome text.</description></item>
/// <item><description><see cref="ResolveCode"/> — three hard-coded test codes instead of the Engine default's single one.</description></item>
/// <item><description><see cref="Users"/> — three hard-coded built-in user names matching those codes.</description></item>
/// <item><description><see cref="Priorities"/>/<see cref="BlockedCombinations"/> — three priority levels and both blocked-combination kinds.</description></item>
/// <item><description><see cref="GetPrintCount"/> — prints an alert message twice and every other received message once.</description></item>
/// <item><description><see cref="ConfigFileEnabled"/> — <see langword="true"/>, so Sample honors a <c>--config</c> argument, unlike the Engine default.</description></item>
/// </list>
/// Actual alarm sound playback and printer discovery/driving are real platform behavior always provided by
/// the engine itself, not something Sample overrides here.
/// </summary>
/// <param name="currentUserProvider">Tracks the user name of the currently running instance, forwarded to the base class.</param>
public sealed class SampleEngineController(ICurrentUserProvider currentUserProvider) : DefaultEngineController<SampleMessage>(currentUserProvider)
{
    private readonly Dictionary<string, UserInfo> userCodes = new()
    {
        ["CODE1"] = new UserInfo { Name = "TEST1", Code = "CODE1", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE2"] = new UserInfo { Name = "TEST2", Code = "CODE2", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE3"] = new UserInfo { Name = "TEST3", Code = "CODE3", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" }
    };

    /// <inheritdoc />
    public override string HomeText => "Select a folder and entry to get started, or create a new draft or note.";
    /// <inheritdoc />
    public override IReadOnlyList<string> Users { get; } = ["TEST1", "TEST2", "TEST3"];
    /// <inheritdoc />
    public override IReadOnlyList<MessagePriorityOption> Priorities { get; } =
    [
        new MessagePriorityOption { Name = "Low", Value = 0 },
        new MessagePriorityOption { Name = "Medium", Value = 1 },
        new MessagePriorityOption { Name = "High", Value = 2 }
    ];
    /// <inheritdoc />
    public override IReadOnlyList<TagPriorityBlock> BlockedCombinations { get; } =
    [
        // Blocks the "SPAM" tag regardless of priority.
        new TagPriorityBlock { Tag = "SPAM", Priority = null },
        // Blocks High priority (value 2) regardless of tag.
        new TagPriorityBlock { Tag = null, Priority = 2 }
    ];
    /// <inheritdoc />
    public override bool ConfigFileEnabled => true;

    /// <inheritdoc />
    protected override string GetMessageId(SampleMessage message) => message.Id;
    /// <inheritdoc />
    protected override void SetMessageId(SampleMessage message, string value) => message.Id = value;
    /// <inheritdoc />
    protected override string GetFromUser(SampleMessage message) => message.Sender;
    /// <inheritdoc />
    protected override void SetFromUser(SampleMessage message, string value) => message.Sender = value;
    /// <inheritdoc />
    protected override string GetSubject(SampleMessage message) => message.Title;
    /// <inheritdoc />
    protected override void SetSubject(SampleMessage message, string value) => message.Title = value;
    /// <inheritdoc />
    protected override string GetBody(SampleMessage message) => message.Text;
    /// <inheritdoc />
    protected override void SetBody(SampleMessage message, string value) => message.Text = value;

    /// <inheritdoc />
    protected override List<MessageAddress> GetAddresses(SampleMessage message)
        => message.Recipients
            .Select(r => new MessageAddress { UserName = r.User, Type = r.IsCc ? AddressType.Cc : AddressType.To })
            .ToList();

    /// <inheritdoc />
    protected override void SetAddresses(SampleMessage message, List<MessageAddress> value)
        => message.Recipients = value
            .Select(a => new SampleRecipient { User = a.UserName, IsCc = a.Type == AddressType.Cc })
            .ToList();

    /// <inheritdoc />
    protected override DateTime GetSentAt(SampleMessage message) => message.Timestamp;
    /// <inheritdoc />
    protected override void SetSentAt(SampleMessage message, DateTime value) => message.Timestamp = value;
    /// <inheritdoc />
    protected override string GetConfirmationMessageId(SampleMessage message) => message.ConfirmsId;
    /// <inheritdoc />
    protected override void SetConfirmationMessageId(SampleMessage message, string value) => message.ConfirmsId = value;
    /// <inheritdoc />
    protected override bool GetIsAlert(SampleMessage message) => message.Alert;
    /// <inheritdoc />
    protected override void SetIsAlert(SampleMessage message, bool value) => message.Alert = value;
    /// <inheritdoc />
    protected override int GetPriority(SampleMessage message) => message.Importance;
    /// <inheritdoc />
    protected override void SetPriority(SampleMessage message, int value) => message.Importance = value;
    /// <inheritdoc />
    protected override string GetTag(SampleMessage message) => message.Category;
    /// <inheritdoc />
    protected override void SetTag(SampleMessage message, string value) => message.Category = value;

    /// <inheritdoc />
    public override UserInfo? ResolveCode(string userCode) => userCodes.GetValueOrDefault(userCode.ToUpperInvariant());

    /// <inheritdoc />
    public override int GetPrintCount(SampleMessage message) => GetIsAlert(message) ? 2 : 1;
}
