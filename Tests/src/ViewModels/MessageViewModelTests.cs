namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="MessageViewModel"/>.</summary>
public sealed class MessageViewModelTests
{
    private static readonly IMessageFormat Format = new TestMessageFormat();

    private static MessageEntity MakeEntity(
        string? messageId = null,
        string subject = "Test Subject",
        string body = "Test body",
        string fromSite = "SENDER",
        AddressData[]? addresses = null,
        DeliveryStatus[]? deliveryStatuses = null)
    {
        string id = messageId ?? Guid.NewGuid().ToString("N").ToUpperInvariant();
        object message = Format.CreateMessage();
        Format.SetMessageId(message, id);
        Format.SetSubject(message, subject);
        Format.SetBody(message, body);
        Format.SetFromSite(message, fromSite);
        Format.SetAddresses(message, [.. (addresses ?? [new AddressData { SiteName = "DEST", Type = "To" }])
            .Select(a => new MessageAddress { SiteName = a.SiteName, Type = a.Type.ParseAddressType() })]);
        return new MessageEntity
        {
            MessageId = id,
            Message = message,
            ReceivedAt = new DateTime(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc),
            DeliveryStatuses = [.. (deliveryStatuses ?? [])]
        };
    }

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>ViewModel exposes all message fields from the entity.</summary>
    [Fact]
    public void Ctor_ExposesEntityFields()
    {
        MessageEntity entity = MakeEntity(
            messageId: "ABC123",
            subject: "Hello",
            body: "World",
            fromSite: "ALPHA",
            addresses:
            [
                new AddressData { SiteName = "BETA", Type = "To" },
                new AddressData { SiteName = "GAMMA", Type = "Cc" }
            ]);

        MessageViewModel vm = new(entity, Format);

        Assert.Equal("ABC123", vm.MessageId);
        Assert.Equal("Hello", vm.Subject);
        Assert.Equal("World", vm.Body);
        Assert.Equal("ALPHA", vm.FromSite);
        Assert.Equal("BETA", vm.ToList);
        Assert.Equal("GAMMA", vm.CcList);
        Assert.Equal(new DateTime(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc), vm.ReceivedAt);
    }

    /// <summary>HasDeliveryStatuses is false when the entity has no delivery statuses.</summary>
    [Fact]
    public void Ctor_NoDeliveryStatuses_HasDeliveryStatusesIsFalse()
    {
        MessageViewModel vm = new(MakeEntity(), Format);

        Assert.False(vm.HasDeliveryStatuses);
        Assert.Empty(vm.DeliveryStatuses);
    }

    /// <summary>HasDeliveryStatuses is true and rows are populated from entity data.</summary>
    [Fact]
    public void Ctor_WithDeliveryStatuses_PopulatesRows()
    {
        DeliveryStatus status = new()
        {
            SiteName = "DEST",
            Status = DestinationStatus.Sending,
            AddressedVia = []
        };
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: [status]), Format);

        Assert.True(vm.HasDeliveryStatuses);
        Assert.Single(vm.DeliveryStatuses);
        Assert.Equal("DEST", vm.DeliveryStatuses[0].SiteName);
        Assert.Equal(DestinationStatus.Sending, vm.DeliveryStatuses[0].Status);
    }

    // ── UpdateDeliveryStatus ──────────────────────────────────────────────────

    /// <summary>UpdateDeliveryStatus updates the matching row's status.</summary>
    [Fact]
    public void UpdateDeliveryStatus_UpdatesMatchingRow()
    {
        DeliveryStatus status = new() { SiteName = "DEST", Status = DestinationStatus.Sending, AddressedVia = [] };
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: [status]), Format);

        vm.UpdateDeliveryStatus("DEST", DestinationStatus.Confirmed);

        Assert.Equal(DestinationStatus.Confirmed, vm.DeliveryStatuses[0].Status);
    }

    /// <summary>UpdateDeliveryStatus recomputes OverallStatus to Confirmed when all sites confirmed.</summary>
    [Fact]
    public void UpdateDeliveryStatus_AllConfirmed_OverallStatusIsConfirmed()
    {
        DeliveryStatus[] statuses =
        [
            new() { SiteName = "A", Status = DestinationStatus.Sending, AddressedVia = [] },
            new() { SiteName = "B", Status = DestinationStatus.Confirmed, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), Format);

        vm.UpdateDeliveryStatus("A", DestinationStatus.Confirmed);

        Assert.Equal(DestinationStatus.Confirmed, vm.OverallStatus);
    }

    /// <summary>Failed takes priority over Confirmed in overall status.</summary>
    [Fact]
    public void UpdateDeliveryStatus_OneFailed_OverallStatusIsFailed()
    {
        DeliveryStatus[] statuses =
        [
            new() { SiteName = "A", Status = DestinationStatus.Confirmed, AddressedVia = [] },
            new() { SiteName = "B", Status = DestinationStatus.Sending, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), Format);

        vm.UpdateDeliveryStatus("B", DestinationStatus.Failed);

        Assert.Equal(DestinationStatus.Failed, vm.OverallStatus);
    }

    // ── ToggleDelivery ────────────────────────────────────────────────────────

    /// <summary>ToggleDelivery flips IsDeliveryExpanded and changes the indicator glyph.</summary>
    [Fact]
    public void ToggleDelivery_FlipsExpandedAndUpdatesIndicator()
    {
        MessageViewModel vm = new(MakeEntity(), Format);
        Assert.False(vm.IsDeliveryExpanded);
        Assert.Equal("▼", vm.DeliveryExpandIndicator);

        vm.ToggleDeliveryCommand.Execute(null);

        Assert.True(vm.IsDeliveryExpanded);
        Assert.Equal("▲", vm.DeliveryExpandIndicator);
    }

    // ── OverallStatusText ─────────────────────────────────────────────────────

    /// <summary>OverallStatusText returns the uppercase status name.</summary>
    [Fact]
    public void OverallStatusText_ReturnsUppercaseStatusName()
    {
        DeliveryStatus[] statuses = [new() { SiteName = "DEST", Status = DestinationStatus.Confirmed, AddressedVia = [] }];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), Format);

        Assert.Equal("CONFIRMED", vm.OverallStatusText);
    }

    /// <summary>OverallStatusText returns empty string when there are no delivery statuses.</summary>
    [Fact]
    public void OverallStatusText_Null_ReturnsEmptyString()
    {
        MessageViewModel vm = new(MakeEntity(), Format);

        Assert.Equal(string.Empty, vm.OverallStatusText);
    }
}
