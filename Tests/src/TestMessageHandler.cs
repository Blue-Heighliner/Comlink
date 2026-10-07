namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IMessageHandler{TFrame, TPriority, TLevel, TAspect}"/> for <see cref="TestFrame"/>: every frame not marked hidden is a message.</summary>
public sealed class TestMessageHandler : IMessageHandler<TestFrame, TestMessagePriority, TestLevel, TestAspect>
{
    /// <summary>Gets how the next message identifier follows the previous one, or <see langword="null"/> for the default GUIDs.</summary>
    public Func<string?, string>? Ids { get; init; }

    /// <inheritdoc />
    public int AlertHistoryLimit => 2;

    /// <inheritdoc />
    public bool FilterAlerts(IReadOnlyList<TestFrame> previousAlerts, TestFrame received)
        => received.Body != "DUPLICATE" || !previousAlerts.Any(previous => previous.Body == "DUPLICATE");

    /// <inheritdoc />
    public bool IsValid(TestFrame frame) => !frame.IsHidden;

    /// <inheritdoc />
    public TestFrame Create(MessageCreateContext<TestMessagePriority, TestLevel, TestAspect> context)
        => new()
        {
            SentAt = context.SentAt,
            Body = context.Body,
            Priority = context.Priority.ToString().ToUpperInvariant(),
            Tag = context.Tag,
            MessageLevel = context.MessageLevel?.ToString().ToUpperInvariant() ?? string.Empty,
            MessageAspect = context.MessageAspect?.ToString() ?? string.Empty
        };

    /// <inheritdoc />
    public string GetId(TestFrame frame) => frame.MessageId;

    /// <inheritdoc />
    public void SetId(TestFrame frame, string id) => frame.MessageId = id;

    /// <inheritdoc />
    public string NextId(string? previous) => Ids?.Invoke(previous) ?? Guid.NewGuid().ToString("N").ToUpperInvariant();

    /// <inheritdoc />
    public IEnumerable<(string Name, AddressType Type, string Information)> GetAddresses(TestFrame frame)
        => frame.Addresses.Select(address => (address.UserName, address.Type.ParseAddressType(), address.Information));

    /// <inheritdoc />
    public void SetAddresses(TestFrame frame, IReadOnlyList<(string Name, AddressType Type, string Information)> addresses)
        => frame.Addresses = [.. addresses.Select(address => new TestAddressEntry { UserName = address.Name, Type = address.Type.ToString(), Information = address.Information })];

    /// <inheritdoc />
    public string GetSender(TestFrame frame) => frame.FromUser;

    /// <inheritdoc />
    public void SetSender(TestFrame frame, string sender) => frame.FromUser = sender;

    /// <inheritdoc />
    public DateTime GetSentAt(TestFrame frame) => frame.SentAt;

    /// <inheritdoc />
    public string GetBody(TestFrame frame) => frame.Body;

    /// <inheritdoc />
    public bool IsAlert(TestFrame frame) => frame.IsAlert || frame.Tag == "ALERT";

    /// <inheritdoc />
    public TestMessagePriority GetPriority(TestFrame frame) => Enum.TryParse(frame.Priority, ignoreCase: true, out TestMessagePriority priority) ? priority : TestMessagePriority.Normal;

    /// <inheritdoc />
    public string GetTag(TestFrame frame) => frame.Tag;

    /// <inheritdoc />
    public TestAspect? GetMessageAspect(TestFrame frame) => Enum.TryParse(frame.MessageAspect, ignoreCase: true, out TestAspect aspect) ? aspect : null;

    /// <inheritdoc />
    public TestLevel? GetMessageLevel(TestFrame frame) => Enum.TryParse(frame.MessageLevel, ignoreCase: true, out TestLevel level) ? level : null;
}
