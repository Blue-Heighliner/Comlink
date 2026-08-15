namespace BlueHeighliner.Comlink.Tests.ViewModels;

/// <summary>Unit tests for <see cref="MessageViewModel"/>.</summary>
public sealed class MessageViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();

    private static MessageEntity MakeEntity(
        string? messageId = null,
        string subject = "Test Subject",
        string body = "Test body",
        string fromUser = "SENDER",
        AddressData[]? addresses = null,
        DeliveryStatus[]? deliveryStatuses = null)
    {
        string id = messageId ?? Guid.NewGuid().ToString("N").ToUpperInvariant();
        object message = format.CreateMessage();
        format.SetMessageId(message, id);
        format.SetSubject(message, subject);
        format.SetBody(message, body);
        format.SetFromUser(message, fromUser);
        format.SetAddresses(message, [.. (addresses ?? [new AddressData { UserName = "DEST", Type = "To" }])
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType() })]);
        return new MessageEntity
        {
            MessageId = id,
            Message = message,
            ReceivedAt = new DateTime(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc),
            DeliveryStatuses = [.. (deliveryStatuses ?? [])]
        };
    }

    /// <summary>ViewModel exposes all message fields from the entity.</summary>
    [Fact]
    public void Ctor_ExposesEntityFields()
    {
        MessageEntity entity = MakeEntity(
            messageId: "ABC123",
            subject: "Hello",
            body: "World",
            fromUser: "ALPHA",
            addresses:
            [
                new AddressData { UserName = "BETA", Type = "To" },
                new AddressData { UserName = "GAMMA", Type = "Cc" }
            ]);

        MessageViewModel vm = new(entity, format);

        Assert.Equal("ABC123", vm.MessageId);
        Assert.Equal("Hello", vm.Subject);
        Assert.Equal("World", vm.Body);
        Assert.Equal("ALPHA", vm.FromUser);
        Assert.Equal("BETA", vm.ToList);
        Assert.Equal("GAMMA", vm.CcList);
        Assert.Equal(new DateTime(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc), vm.ReceivedAt);
    }

    /// <summary>HasDeliveryStatuses is false when the entity has no delivery statuses.</summary>
    [Fact]
    public void Ctor_NoDeliveryStatuses_HasDeliveryStatusesIsFalse()
    {
        MessageViewModel vm = new(MakeEntity(), format);

        Assert.False(vm.HasDeliveryStatuses);
        Assert.Empty(vm.DeliveryStatuses);
    }

    /// <summary>HasDeliveryStatuses is true and rows are populated from entity data.</summary>
    [Fact]
    public void Ctor_WithDeliveryStatuses_PopulatesRows()
    {
        DeliveryStatus status = new()
        {
            UserName = "DEST",
            Status = DestinationStatus.Sending,
            AddressedVia = []
        };
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: [status]), format);

        Assert.True(vm.HasDeliveryStatuses);
        Assert.Single(vm.DeliveryStatuses);
        Assert.Equal("DEST", vm.DeliveryStatuses[0].UserName);
        Assert.Equal(DestinationStatus.Sending, vm.DeliveryStatuses[0].Status);
    }

    /// <summary>UpdateDeliveryStatus updates the matching row's status.</summary>
    [Fact]
    public void UpdateDeliveryStatus_UpdatesMatchingRow()
    {
        DeliveryStatus status = new() { UserName = "DEST", Status = DestinationStatus.Sending, AddressedVia = [] };
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: [status]), format);

        vm.UpdateDeliveryStatus("DEST", DestinationStatus.Confirmed);

        Assert.Equal(DestinationStatus.Confirmed, vm.DeliveryStatuses[0].Status);
    }

    /// <summary>UpdateDeliveryStatus recomputes OverallStatus to Confirmed when all users confirmed.</summary>
    [Fact]
    public void UpdateDeliveryStatus_AllConfirmed_OverallStatusIsConfirmed()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Sending, AddressedVia = [] },
            new() { UserName = "B", Status = DestinationStatus.Confirmed, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("A", DestinationStatus.Confirmed);

        Assert.Equal(DestinationStatus.Confirmed, vm.OverallStatus);
    }

    /// <summary>Failed takes priority over Confirmed in overall status.</summary>
    [Fact]
    public void UpdateDeliveryStatus_OneFailed_OverallStatusIsFailed()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Confirmed, AddressedVia = [] },
            new() { UserName = "B", Status = DestinationStatus.Sending, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("B", DestinationStatus.Failed);

        Assert.Equal(DestinationStatus.Failed, vm.OverallStatus);
    }

    /// <summary>ToggleDelivery flips IsDeliveryExpanded and changes the indicator glyph.</summary>
    [Fact]
    public void ToggleDelivery_FlipsExpandedAndUpdatesIndicator()
    {
        MessageViewModel vm = new(MakeEntity(), format);
        Assert.False(vm.IsDeliveryExpanded);
        Assert.Equal("▼", vm.DeliveryExpandIndicator);

        vm.ToggleDeliveryCommand.Execute(null);

        Assert.True(vm.IsDeliveryExpanded);
        Assert.Equal("▲", vm.DeliveryExpandIndicator);
    }

    /// <summary>OverallStatusText returns the uppercase status name.</summary>
    [Fact]
    public void OverallStatusText_ReturnsUppercaseStatusName()
    {
        DeliveryStatus[] statuses = [new() { UserName = "DEST", Status = DestinationStatus.Confirmed, AddressedVia = [] }];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        Assert.Equal("CONFIRMED", vm.OverallStatusText);
    }

    /// <summary>OverallStatusText returns empty string when there are no delivery statuses.</summary>
    [Fact]
    public void OverallStatusText_Null_ReturnsEmptyString()
    {
        MessageViewModel vm = new(MakeEntity(), format);

        Assert.Equal(string.Empty, vm.OverallStatusText);
    }

    /// <summary>UpdateDeliveryStatus recomputes OverallStatus to Read only once every user has read the message.</summary>
    [Fact]
    public void UpdateDeliveryStatus_AllRead_OverallStatusIsRead()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Read, AddressedVia = [] },
            new() { UserName = "B", Status = DestinationStatus.Confirmed, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("B", DestinationStatus.Read);

        Assert.Equal(DestinationStatus.Read, vm.OverallStatus);
    }

    /// <summary>OverallStatus stays Confirmed while one user has read and another has only confirmed.</summary>
    [Fact]
    public void UpdateDeliveryStatus_OneReadOneConfirmed_OverallStatusIsConfirmed()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Sending, AddressedVia = [] },
            new() { UserName = "B", Status = DestinationStatus.Confirmed, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("A", DestinationStatus.Read);

        Assert.Equal(DestinationStatus.Confirmed, vm.OverallStatus);
    }

    /// <summary>ReadStatus and ReadStatusText reflect the entity's own Inbox read status.</summary>
    [Fact]
    public void Ctor_InboundEntity_ExposesReadStatus()
    {
        MessageEntity entity = MakeEntity();
        entity.ReadStatus = DestinationStatus.Received;

        MessageViewModel vm = new(entity, format);

        Assert.Equal(DestinationStatus.Received, vm.ReadStatus);
        Assert.Equal("RECEIVED", vm.ReadStatusText);
    }

    /// <summary>ReadStatusText is empty when ReadStatus is null (an Outbox message).</summary>
    [Fact]
    public void ReadStatusText_Null_ReturnsEmptyString()
    {
        MessageViewModel vm = new(MakeEntity(), format);

        Assert.Null(vm.ReadStatus);
        Assert.Equal(string.Empty, vm.ReadStatusText);
    }

    /// <summary>Setting ReadStatus directly updates ReadStatusText.</summary>
    [Fact]
    public void ReadStatus_SetDirectly_UpdatesReadStatusText()
    {
        MessageViewModel vm = new(MakeEntity(), format);

        vm.ReadStatus = DestinationStatus.Read;

        Assert.Equal("READ", vm.ReadStatusText);
    }

    /// <summary>IsAlert reflects the message's alert flag.</summary>
    [Fact]
    public void Ctor_AlertMessage_IsAlertIsTrue()
    {
        MessageEntity entity = MakeEntity();
        format.SetIsAlert(entity.Message, true);

        MessageViewModel vm = new(entity, format);

        Assert.True(vm.IsAlert);
    }
}
