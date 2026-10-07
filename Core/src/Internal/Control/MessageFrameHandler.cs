namespace BlueHeighliner.Comlink;

/// <summary>The engine's untyped view of the host's <see cref="IMessageHandler{TFrame, TPriority, TLevel, TAspect}"/>, working on frames as <see cref="object"/>.</summary>
internal interface IMessageFrameHandler
{
    /// <summary>Returns whether <paramref name="frame"/> is a message.</summary>
    bool IsValid(object frame);
    /// <summary>Creates a message frame carrying <paramref name="content"/>.</summary>
    object Create(MessageContent content);
    /// <summary>Gets the recipient list of <paramref name="frame"/>.</summary>
    List<MessageAddress> GetAddresses(object frame);
    /// <summary>Sets the recipient list of <paramref name="frame"/>.</summary>
    void SetAddresses(object frame, List<MessageAddress> addresses);
    /// <summary>Gets the sender of <paramref name="frame"/>.</summary>
    string GetSender(object frame);
    /// <summary>Sets the sender of <paramref name="frame"/>.</summary>
    void SetSender(object frame, string sender);
    /// <summary>Gets the identifier of the message <paramref name="frame"/>.</summary>
    string GetId(object frame);
    /// <summary>Sets the identifier of the message <paramref name="frame"/>.</summary>
    void SetId(object frame, string id);
    /// <summary>Generates the next message identifier.</summary>
    string NextId(string? previous);
    /// <summary>Gets the sent time of <paramref name="frame"/>.</summary>
    DateTime GetSentAt(object frame);
    /// <summary>Gets the body text of <paramref name="frame"/>.</summary>
    string GetBody(object frame);
    /// <summary>Returns whether <paramref name="frame"/> is an alert.</summary>
    bool IsAlert(object frame);
    /// <summary>Gets the names of the keys that open the oldest unread alert.</summary>
    IReadOnlyList<string> AlertQuickReadKeys { get; }
    /// <summary>Gets how many alerts are kept for <see cref="FilterAlerts"/>.</summary>
    int AlertHistoryLimit { get; }
    /// <summary>Returns whether the alert <paramref name="received"/> is kept, given the alerts kept before it.</summary>
    /// <param name="previousAlerts">The alerts kept before it, oldest first.</param>
    /// <param name="received">The received alert.</param>
    bool FilterAlerts(IReadOnlyList<object> previousAlerts, object received);
    /// <summary>Gets the priority number of <paramref name="frame"/>.</summary>
    Enum GetPriority(object frame);
    /// <summary>Gets the tag of <paramref name="frame"/>.</summary>
    string GetTag(object frame);
    /// <summary>Gets the message level name of <paramref name="frame"/>, or an empty string for none.</summary>
    string GetMessageLevel(object frame);
    /// <summary>Gets the message level <paramref name="frame"/> carries as the host's enum member, whether or not it is a configured one, or <see langword="null"/> for none.</summary>
    Enum? GetMessageLevelKey(object frame);
    /// <summary>Gets the message aspect name of <paramref name="frame"/>, or an empty string for none.</summary>
    string GetMessageAspect(object frame);
}

/// <summary>Adapts a typed <see cref="IMessageHandler{TFrame, TPriority, TLevel, TAspect}"/> to <see cref="IMessageFrameHandler"/>.</summary>
internal sealed class MessageFrameHandler<TFrame, TPriority, TLevel, TAspect>(IMessageHandler<TFrame, TPriority, TLevel, TAspect> handler, IReadOnlyList<MessageLevel> messageLevels, IReadOnlyList<MessageAspect> messageAspects) : IMessageFrameHandler where TFrame : class where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
{
    /// <inheritdoc />
    public bool IsValid(object frame) => handler.IsValid((TFrame)frame);

    /// <inheritdoc />
    public object Create(MessageContent content)
        => handler.Create(new MessageCreateContext<TPriority, TLevel, TAspect>
        {
            SentAt = content.SentAt,
            Body = content.Body,
            Priority = (TPriority)(object)content.Priority,
            Tag = content.Tag,
            MessageLevel = ToLevel(content.MessageLevel),
            MessageAspect = ToAspect(content.MessageAspect)
        });

    /// <inheritdoc />
    public List<MessageAddress> GetAddresses(object frame)
        => [.. handler.GetAddresses((TFrame)frame).Select(address => new MessageAddress { UserName = address.Name, Type = address.Type, Information = address.Information })];

    /// <inheritdoc />
    public void SetAddresses(object frame, List<MessageAddress> addresses)
        => handler.SetAddresses((TFrame)frame, [.. addresses.Select(address => (address.UserName, address.Type, address.Information))]);

    /// <inheritdoc />
    public string GetSender(object frame) => handler.GetSender((TFrame)frame);

    /// <inheritdoc />
    public void SetSender(object frame, string sender) => handler.SetSender((TFrame)frame, sender);

    /// <inheritdoc />
    public string GetId(object frame) => handler.GetId((TFrame)frame);

    /// <inheritdoc />
    public void SetId(object frame, string id) => handler.SetId((TFrame)frame, id);

    /// <inheritdoc />
    public string NextId(string? previous) => handler.NextId(previous);

    /// <inheritdoc />
    public DateTime GetSentAt(object frame) => handler.GetSentAt((TFrame)frame);

    /// <inheritdoc />
    public string GetBody(object frame) => handler.GetBody((TFrame)frame);

    /// <inheritdoc />
    public bool IsAlert(object frame) => handler.IsAlert((TFrame)frame);

    /// <inheritdoc />
    public IReadOnlyList<string> AlertQuickReadKeys => handler.AlertQuickReadKeys;

    /// <inheritdoc />
    public int AlertHistoryLimit => handler.AlertHistoryLimit;

    /// <inheritdoc />
    public bool FilterAlerts(IReadOnlyList<object> previousAlerts, object received) => handler.FilterAlerts([.. previousAlerts.Cast<TFrame>()], (TFrame)received);

    /// <inheritdoc />
    public Enum GetPriority(object frame) => handler.GetPriority((TFrame)frame);

    /// <inheritdoc />
    public string GetTag(object frame) => handler.GetTag((TFrame)frame);

    /// <inheritdoc />
    public string GetMessageLevel(object frame)
        => handler.GetMessageLevel((TFrame)frame) is { } level ? messageLevels.FirstOrDefault(candidate => candidate.Key?.Equals(level) == true)?.Name ?? string.Empty : string.Empty;

    /// <inheritdoc />
    public Enum? GetMessageLevelKey(object frame) => handler.GetMessageLevel((TFrame)frame) is { } level ? level : null;

    /// <inheritdoc />
    public string GetMessageAspect(object frame)
        => handler.GetMessageAspect((TFrame)frame) is { } aspect ? messageAspects.FirstOrDefault(candidate => candidate.Key.Equals(aspect))?.Name ?? string.Empty : string.Empty;

    private TAspect? ToAspect(string name)
        => string.IsNullOrEmpty(name) ? null
        : messageAspects.FirstOrDefault(aspect => string.Equals(aspect.Name, name, StringComparison.OrdinalIgnoreCase))?.Key is TAspect key ? key
        : throw new ArgumentException($"The message aspect \"{name}\" is not one of the configured message aspects: {string.Join(", ", messageAspects.Select(aspect => aspect.Name))}", nameof(name));

    private TLevel? ToLevel(string name)
        => string.IsNullOrEmpty(name) ? null
        : messageLevels.FirstOrDefault(level => string.Equals(level.Name, name, StringComparison.OrdinalIgnoreCase))?.Key is TLevel key ? key
        : throw new ArgumentException($"The message level \"{name}\" is not one of the configured message levels: {string.Join(", ", messageLevels.Select(level => level.Name))}", nameof(name));
}
