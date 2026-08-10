namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>ViewModel interface for displaying a received or sent message and its per-site delivery statuses.</summary>
public interface IMessageViewModel
{
    /// <summary>Gets the unique message identifier.</summary>
    string MessageId { get; }
    /// <summary>Gets the message subject line.</summary>
    string Subject { get; }
    /// <summary>Gets the message body text.</summary>
    string Body { get; }
    /// <summary>Gets the name of the site that originated the message.</summary>
    string FromSite { get; }
    /// <summary>Gets a comma-separated list of primary recipient site names.</summary>
    string ToList { get; }
    /// <summary>Gets a comma-separated list of carbon-copy recipient site names.</summary>
    string CcList { get; }
    /// <summary>Gets the timestamp when the message was received or stored.</summary>
    DateTime ReceivedAt { get; }
    /// <summary>Gets a value indicating whether this message has any per-site delivery status rows.</summary>
    bool HasDeliveryStatuses { get; }
    /// <summary>Gets the observable collection of per-site delivery status rows.</summary>
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
    /// <summary>Updates the delivery status row for the specified site and recomputes the overall status.</summary>
    void UpdateDeliveryStatus(string siteName, DestinationStatus status);
}

/// <summary>Represents the delivery status for a single recipient site within a sent message.</summary>
public sealed partial class DeliveryStatusRow : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private DestinationStatus _status;

    /// <summary>Gets the name of the destination site.</summary>
    public string SiteName { get; }
    /// <summary>Gets the display name including addressed group context (e.g. <c>SITE1 (GROUP1)</c>).</summary>
    public string DisplayName { get; }
    /// <summary>Gets the uppercase display string for the current delivery status.</summary>
    public string StatusText => Status.ToString().ToUpperInvariant();

    /// <summary>Initializes a new row for the given site, initial status, and addressed group context.</summary>
    /// <param name="siteName">Name of the destination site.</param>
    /// <param name="status">Initial delivery status.</param>
    /// <param name="addressedVia">Group names from the address list that contained this site.</param>
    public DeliveryStatusRow(string siteName, DestinationStatus status, IReadOnlyList<string> addressedVia)
    {
        SiteName = siteName;
        _status = status;
        DisplayName = addressedVia.Count > 0
            ? $"{siteName} ({string.Join(", ", addressedVia)})"
            : siteName;
    }
}

/// <summary>ViewModel for displaying a received or sent message and its per-site delivery statuses.</summary>
public sealed partial class MessageViewModel : ObservableObject, IMessageViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OverallStatusText))]
    private DestinationStatus? _overallStatus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeliveryExpandIndicator))]
    private bool _isDeliveryExpanded;

    /// <summary>Gets the unique message identifier.</summary>
    public string MessageId { get; }
    /// <summary>Gets the message subject line.</summary>
    public string Subject { get; }
    /// <summary>Gets the message body text.</summary>
    public string Body { get; }
    /// <summary>Gets the name of the site that originated the message.</summary>
    public string FromSite { get; }
    /// <summary>Gets a comma-separated list of primary recipient site names.</summary>
    public string ToList { get; }
    /// <summary>Gets a comma-separated list of carbon-copy recipient site names.</summary>
    public string CcList { get; }
    /// <summary>Gets the timestamp when the message was received or stored.</summary>
    public DateTime ReceivedAt { get; }
    /// <summary>Gets a value indicating whether this message has any per-site delivery status rows.</summary>
    public bool HasDeliveryStatuses { get; }
    /// <summary>Gets the observable collection of per-site delivery status rows.</summary>
    public ObservableCollection<DeliveryStatusRow> DeliveryStatuses { get; } = [];
    /// <summary>Gets the uppercase display text for the overall delivery status.</summary>
    public string OverallStatusText => OverallStatus?.ToString().ToUpperInvariant() ?? string.Empty;
    /// <summary>Gets the expand/collapse indicator glyph for the delivery status section.</summary>
    public string DeliveryExpandIndicator => IsDeliveryExpanded ? "▲" : "▼";

    /// <summary>Initializes the ViewModel from the given message entity.</summary>
    /// <param name="entity">The message entity to display.</param>
    /// <param name="messageFormat">Maps logical fields onto the entity's stored message.</param>
    public MessageViewModel(MessageEntity entity, IMessageFormat messageFormat)
    {
        MessageId = entity.MessageId;
        Subject = messageFormat.GetSubject(entity.Message);
        Body = messageFormat.GetBody(entity.Message);
        FromSite = messageFormat.GetFromSite(entity.Message);
        ReceivedAt = entity.ReceivedAt;
        List<MessageAddress> addresses = messageFormat.GetAddresses(entity.Message);
        ToList = string.Join(", ", addresses.Where(a => a.Type == AddressType.To).Select(a => a.SiteName));
        CcList = string.Join(", ", addresses.Where(a => a.Type == AddressType.Cc).Select(a => a.SiteName));
        foreach (DeliveryStatus d in entity.DeliveryStatuses)
            DeliveryStatuses.Add(new DeliveryStatusRow(d.SiteName, d.Status, d.AddressedVia));
        HasDeliveryStatuses = DeliveryStatuses.Count > 0;
        _overallStatus = entity.OverallStatus;
    }

    /// <summary>Toggles the delivery status section between expanded and collapsed.</summary>
    [RelayCommand]
    private void ToggleDelivery() => IsDeliveryExpanded = !IsDeliveryExpanded;

    /// <summary>Updates the delivery status row for the specified site and recomputes the overall status.</summary>
    /// <param name="siteName">Name of the site whose status changed.</param>
    /// <param name="status">New delivery status for the site.</param>
    public void UpdateDeliveryStatus(string siteName, DestinationStatus status)
    {
        DeliveryStatusRow? row = DeliveryStatuses.FirstOrDefault(r => r.SiteName == siteName);
        if (row is not null) row.Status = status;
        OverallStatus = ComputeOverallStatus();
    }

    private DestinationStatus? ComputeOverallStatus()
    {
        if (DeliveryStatuses.Count == 0) return null;
        if (DeliveryStatuses.Any(d => d.Status == DestinationStatus.Failed)) return DestinationStatus.Failed;
        if (DeliveryStatuses.All(d => d.Status == DestinationStatus.Confirmed)) return DestinationStatus.Confirmed;
        if (DeliveryStatuses.All(d => d.Status != DestinationStatus.Sending)) return DestinationStatus.Sent;
        return DestinationStatus.Sending;
    }
}
