namespace BlueHeighliner.Comlink;

/// <summary>ViewModel interface for displaying a received or sent message and its per-user delivery statuses.</summary>
internal interface IMessageViewModel
{
    /// <summary>Gets the unique message identifier.</summary>
    string MessageId { get; }
    /// <summary>Gets a value indicating whether this is the sent (Outbox) record of the message rather than the received (Inbox) one.</summary>
    bool IsOutbound { get; }
    /// <summary>Gets the message body text.</summary>
    string Body { get; }
    /// <summary>Gets the name of the user that originated the message.</summary>
    string FromUser { get; }
    /// <summary>Gets the uppercase section header label for <see cref="ToList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    string ToLabel { get; }
    /// <summary>Gets a comma-separated list of primary recipients, each followed by its custom instructions when it has any.</summary>
    string ToList { get; }
    /// <summary>Gets the uppercase section header label for <see cref="CcList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    string CcLabel { get; }
    /// <summary>Gets a comma-separated list of carbon-copy recipients, each followed by its custom instructions when it has any.</summary>
    string CcList { get; }
    /// <summary>Gets the uppercase section header label for <see cref="ExternalList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    string ExternalLabel { get; }
    /// <summary>Gets a comma-separated list of the addresses outside the system, each followed by its custom instructions when it has any. These are information for the user only; nothing is delivered to them.</summary>
    string ExternalList { get; }
    /// <summary>Gets the timestamp when the message was received or stored.</summary>
    DateTime ReceivedAt { get; }
    /// <summary>Gets a value indicating whether this message is an alert.</summary>
    bool IsAlert { get; }
    /// <summary>Gets the priority label this message was sent at; see <see cref="IEngineController.Priorities"/>.</summary>
    string PriorityLabel { get; }
    /// <summary>Gets a value indicating whether the tag field is shown; see <see cref="IEngineController.TagsEnabled"/>.</summary>
    bool TagsEnabled { get; }
    /// <summary>Gets the short, user-inputted tag identifying the type of this message, or an empty string if none was set.</summary>
    string Tag { get; }
    /// <summary>Gets the message level name this message was sent at, or an empty string when no message levels are configured.</summary>
    string MessageLevelName { get; }
    /// <summary>Gets the hex color for <see cref="MessageLevelName"/>, or <see langword="null"/> when it has none recognized.</summary>
    string? MessageLevelColorHex { get; }
    /// <summary>Gets the message aspect name this message carries, or an empty string for none.</summary>
    string MessageAspectName { get; }
    /// <summary>
    /// Gets or sets this Inbox message's own read status (<c>Received</c>/<c>Read</c>); <see langword="null"/>
    /// for an Outbox message, which tracks read state per-destination in <see cref="DeliveryStatuses"/> instead.
    /// </summary>
    DestinationStatus? ReadStatus { get; set; }
    /// <summary>Gets the uppercase display text for <see cref="ReadStatus"/>, or empty if <see langword="null"/>.</summary>
    string ReadStatusText { get; }
    /// <summary>Gets a value indicating whether this message has any per-user delivery status rows.</summary>
    bool HasDeliveryStatuses { get; }
    /// <summary>Gets the observable collection of per-user delivery status rows.</summary>
    ObservableCollection<DeliveryStatusRow> DeliveryStatuses { get; }
    /// <summary>Gets or sets the overall delivery status across all destinations.</summary>
    DestinationStatus? OverallStatus { get; set; }
    /// <summary>Gets the uppercase display text for the overall delivery status.</summary>
    string OverallStatusText { get; }
    /// <summary>Gets or sets a value indicating whether the delivery status panel is expanded.</summary>
    bool IsDeliveryExpanded { get; set; }
    /// <summary>Gets the expand/collapse indicator glyph for the delivery status section.</summary>
    string DeliveryExpandIndicator { get; }
    /// <summary>Toggles the delivery status section between expanded and collapsed.</summary>
    IRelayCommand ToggleDeliveryCommand { get; }
    /// <summary>Updates the delivery status row for the specified user and recomputes the overall status.</summary>
    void UpdateDeliveryStatus(string userName, DestinationStatus status);
}

/// <summary>Represents the delivery status for a single recipient user within a sent message.</summary>
internal sealed partial class DeliveryStatusRow : ObservableObject
{
    /// <summary>Initializes a new row for the given user, initial status, and addressed group context.</summary>
    /// <param name="userName">Name of the destination user.</param>
    /// <param name="status">Initial delivery status.</param>
    /// <param name="addressedVia">Group names from the address list that contained this user.</param>
    public DeliveryStatusRow(string userName, DestinationStatus status, IReadOnlyList<string> addressedVia)
    {
        UserName = userName;
        this.status = status;
        DisplayName = addressedVia.Count > 0
            ? $"{userName} ({string.Join(", ", addressedVia)})"
            : userName;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private DestinationStatus status;

    /// <summary>Gets the name of the destination user.</summary>
    public string UserName { get; }
    /// <summary>Gets the display name including addressed group context (e.g. <c>USER1 (GROUP1)</c>).</summary>
    public string DisplayName { get; }
    /// <summary>Gets the uppercase display string for the current delivery status.</summary>
    public string StatusText => Status.ToString().ToUpperInvariant();
}

/// <summary>ViewModel for displaying a received or sent message and its per-user delivery statuses.</summary>
[ConstructedManually]
internal sealed partial class MessageViewModel : ObservableObject, IMessageViewModel
{
    private static string Describe(List<MessageAddress> addresses, AddressType type)
        => string.Join(", ", addresses
            .Where(a => a.Type == type)
            .Select(a => string.IsNullOrWhiteSpace(a.Information) ? a.UserName : $"{a.UserName} - {a.Information}"));

    /// <summary>Initializes the ViewModel from the given message entity.</summary>
    /// <param name="entity">The message entity to display.</param>
    /// <param name="engineController">Turns the entity's stored priority, message level and message aspect back into their names.</param>
    public MessageViewModel(MessageEntity entity, IEngineController engineController)
    {
        MessageId = entity.MessageId;
        IsOutbound = entity.IsOutbound;
        MessageData message = entity.Message;
        Body = message.Body;
        FromUser = message.FromUser;
        ReceivedAt = entity.ReceivedAt;
        IsAlert = message.IsAlert;
        PriorityLabel = engineController.NameOf(engineController.PriorityOf(message.Priority));
        TagsEnabled = engineController.TagsEnabled;
        Tag = message.Tag;
        MessageLevelName = engineController.NameOfLevel(message);
        MessageLevelColorHex = engineController.MessageLevels.IsRecognized(MessageLevelName) ? engineController.MessageLevels.GetColor(MessageLevelName) : null;
        MessageAspectName = engineController.NameOfAspect(message);
        List<MessageAddress> addresses = [.. message.Addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })];
        IReadOnlyList<AddressTypeOption> addressTypes = engineController.AddressTypes;
        ToLabel = addressTypes.GetLabel(AddressType.To).ToUpperInvariant();
        ToList = Describe(addresses, AddressType.To);
        CcLabel = addressTypes.GetLabel(AddressType.Cc).ToUpperInvariant();
        CcList = Describe(addresses, AddressType.Cc);
        ExternalLabel = addressTypes.GetLabel(AddressType.External).ToUpperInvariant();
        ExternalList = Describe(addresses, AddressType.External);
        foreach (DeliveryStatus d in entity.DeliveryStatuses)
        {
            DeliveryStatuses.Add(new DeliveryStatusRow(d.UserName, d.Status, d.AddressedVia));
        }
        HasDeliveryStatuses = DeliveryStatuses.Count > 0;
        overallStatus = entity.OverallStatus;
        readStatus = entity.ReadStatus;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverallStatusText))]
    private DestinationStatus? overallStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadStatusText))]
    private DestinationStatus? readStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeliveryExpandIndicator))]
    private bool isDeliveryExpanded;

    /// <summary>Gets the unique message identifier.</summary>
    public string MessageId { get; }
    /// <summary>Gets a value indicating whether this is the sent (Outbox) record of the message rather than the received (Inbox) one.</summary>
    public bool IsOutbound { get; }
    /// <summary>Gets the message body text.</summary>
    public string Body { get; }
    /// <summary>Gets the name of the user that originated the message.</summary>
    public string FromUser { get; }
    /// <summary>Gets the uppercase section header label for <see cref="ToList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    public string ToLabel { get; }
    /// <summary>Gets a comma-separated list of primary recipients, each followed by its custom instructions when it has any.</summary>
    public string ToList { get; }
    /// <summary>Gets the uppercase section header label for <see cref="CcList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    public string CcLabel { get; }
    /// <summary>Gets a comma-separated list of carbon-copy recipients, each followed by its custom instructions when it has any.</summary>
    public string CcList { get; }
    /// <summary>Gets the uppercase section header label for <see cref="ExternalList"/>; see <see cref="IEngineController.AddressTypes"/>.</summary>
    public string ExternalLabel { get; }
    /// <summary>Gets a comma-separated list of the addresses outside the system, each followed by its custom instructions when it has any. These are information for the user only; nothing is delivered to them.</summary>
    public string ExternalList { get; }
    /// <summary>Gets the timestamp when the message was received or stored.</summary>
    public DateTime ReceivedAt { get; }
    /// <summary>Gets a value indicating whether this message is an alert.</summary>
    public bool IsAlert { get; }
    /// <summary>Gets the priority label this message was sent at; see <see cref="IEngineController.Priorities"/>.</summary>
    public string PriorityLabel { get; }
    /// <summary>Gets a value indicating whether the tag field is shown; see <see cref="IEngineController.TagsEnabled"/>.</summary>
    public bool TagsEnabled { get; }
    /// <summary>Gets the short, user-inputted tag identifying the type of this message, or an empty string if none was set.</summary>
    public string Tag { get; }
    /// <summary>Gets the message level name this message was sent at, or an empty string when no message levels are configured.</summary>
    public string MessageLevelName { get; }
    /// <summary>Gets the hex color for <see cref="MessageLevelName"/>, or <see langword="null"/> when it has none recognized.</summary>
    public string? MessageLevelColorHex { get; }
    /// <inheritdoc />
    public string MessageAspectName { get; }
    /// <summary>Gets the uppercase display text for <see cref="ReadStatus"/>, or empty if <see langword="null"/>.</summary>
    public string ReadStatusText => ReadStatus?.ToString().ToUpperInvariant() ?? string.Empty;
    /// <summary>Gets a value indicating whether this message has any per-user delivery status rows.</summary>
    public bool HasDeliveryStatuses { get; }
    /// <summary>Gets the observable collection of per-user delivery status rows.</summary>
    public ObservableCollection<DeliveryStatusRow> DeliveryStatuses { get; } = [];
    /// <summary>Gets the uppercase display text for the overall delivery status.</summary>
    public string OverallStatusText => OverallStatus?.ToString().ToUpperInvariant() ?? string.Empty;
    /// <summary>Gets the expand/collapse indicator glyph for the delivery status section.</summary>
    public string DeliveryExpandIndicator => IsDeliveryExpanded ? "▲" : "▼";

    /// <summary>Toggles the delivery status section between expanded and collapsed.</summary>
    [RelayCommand]
    private void ToggleDelivery() => IsDeliveryExpanded = !IsDeliveryExpanded;

    /// <summary>Updates the delivery status row for the specified user and recomputes the overall status.</summary>
    /// <param name="userName">Name of the user whose status changed.</param>
    /// <param name="status">New delivery status for the user.</param>
    public void UpdateDeliveryStatus(string userName, DestinationStatus status)
    {
        DeliveryStatusRow? row = DeliveryStatuses.FirstOrDefault(r => string.Equals(r.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            row.Status = status;
        }
        OverallStatus = ComputeOverallStatus();
    }

    private DestinationStatus? ComputeOverallStatus()
    {
        if (DeliveryStatuses.Count == 0)
        {
            return null;
        }
        if (DeliveryStatuses.Any(d => d.Status is DestinationStatus.Failed))
        {
            return DestinationStatus.Failed;
        }
        if (DeliveryStatuses.All(d => d.Status is DestinationStatus.Read))
        {
            return DestinationStatus.Read;
        }
        if (DeliveryStatuses.All(d => d.Status is DestinationStatus.Received or DestinationStatus.Read))
        {
            return DestinationStatus.Received;
        }
        if (DeliveryStatuses.All(d => d.Status is not DestinationStatus.Sending))
        {
            return DestinationStatus.Sent;
        }
        return DestinationStatus.Sending;
    }
}
