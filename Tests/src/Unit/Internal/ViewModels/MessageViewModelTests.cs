namespace BlueHeighliner.Comlink.Tests.Unit.Internal.ViewModels;

/// <summary>Unit tests for <see cref="MessageViewModel"/>.</summary>
public sealed class MessageViewModelTests
{
    private static readonly IEngineController format = new TestEngineController();

    private static MessageEntity MakeEntity(
        string? messageId = null,
        string body = "Test body",
        string fromUser = "SENDER",
        AddressData[]? addresses = null,
        DeliveryStatus[]? deliveryStatuses = null,
        int priority = 0,
        string tag = "",
        string messageLevel = "")
    {
        string id = messageId ?? Guid.NewGuid().ToString("N").ToUpperInvariant();
        object message = format.CreateFrame();
        ((TestFrame)message).MessageId = id;
        ((TestFrame)message).Body = body;
        format.SetFromUser(message, fromUser);
        ((TestFrame)message).Priority = priority == 0 ? "NORMAL" : $"LEVEL{priority}";
        ((TestFrame)message).Tag = tag;
        ((TestFrame)message).MessageLevel = messageLevel;
        format.SetAddresses(message, [.. (addresses ?? [new AddressData { UserName = "DEST", Type = "To" }])
            .Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })]);
        return new MessageEntity
        {
            MessageId = id,
            Message = message,
            ReceivedAt = new DateTime(2025, 7, 4, 10, 0, 0, DateTimeKind.Utc),
            DeliveryStatuses = [.. (deliveryStatuses ?? [])]
        };
    }

    /// <summary>Recipients are listed by role, with their custom instructions after a dash, and external addresses get their own list.</summary>
    [Fact]
    public void Ctor_ListsRecipientsByRole_WithInformation()
    {
        MessageEntity entity = MakeEntity(addresses:
        [
            new AddressData { UserName = "BETA", Type = "To" },
            new AddressData { UserName = "GAMMA", Type = "To", Information = "Urgent" },
            new AddressData { UserName = "DELTA", Type = "Cc" },
            new AddressData { UserName = "OMAHA", Type = "External", Information = "Deliver to Eastside Office" },
            new AddressData { UserName = "RENO", Type = "External" }
        ]);

        MessageViewModel vm = new(entity, format);

        Assert.Equal("BETA, GAMMA - Urgent", vm.ToList);
        Assert.Equal("DELTA", vm.CcList);
        Assert.Equal("OMAHA - Deliver to Eastside Office, RENO", vm.ExternalList);
    }

    /// <summary>A message with no external addresses has an empty external list, so the view hides its row.</summary>
    [Fact]
    public void Ctor_NoExternalAddresses_ExternalListIsEmpty()
        => Assert.Empty(new MessageViewModel(MakeEntity(), format).ExternalList);

    /// <summary>The section header labels default to the uppercase address type names.</summary>
    [Fact]
    public void Ctor_DefaultAddressTypeLabels_AreUppercaseTypeNames()
    {
        MessageViewModel vm = new(MakeEntity(), format);

        Assert.Equal("TO", vm.ToLabel);
        Assert.Equal("CC", vm.CcLabel);
        Assert.Equal("EXTERNAL", vm.ExternalLabel);
    }

    /// <summary>A host-overridden address type label is reflected, uppercased, in the corresponding section header.</summary>
    [Fact]
    public void Ctor_OverriddenAddressTypeLabel_IsReflectedUppercased()
    {
        Mock<IEngineController> mock = new(MockBehavior.Loose) { CallBase = false };
        mock.Setup(e => e.GetBody(It.IsAny<object>())).Returns(format.GetBody);
        mock.Setup(e => e.GetFromUser(It.IsAny<object>())).Returns(format.GetFromUser);
        mock.Setup(e => e.GetIsAlert(It.IsAny<object>())).Returns(format.GetIsAlert);
        mock.Setup(e => e.GetPriority(It.IsAny<object>())).Returns(format.GetPriority);
        mock.Setup(e => e.GetTag(It.IsAny<object>())).Returns(format.GetTag);
        mock.Setup(e => e.GetMessageLevel(It.IsAny<object>())).Returns(format.GetMessageLevel);
        mock.Setup(e => e.Priorities).Returns(format.Priorities);
        mock.Setup(e => e.TagsEnabled).Returns(format.TagsEnabled);
        mock.Setup(e => e.TagLabel).Returns(format.TagLabel);
        mock.Setup(e => e.MessageLevels).Returns(format.MessageLevels);
        mock.Setup(e => e.GetAddresses(It.IsAny<object>())).Returns(format.GetAddresses);
        mock.Setup(e => e.AddressTypes).Returns([
            new AddressTypeOption { Type = AddressType.To, Label = "To" },
            new AddressTypeOption { Type = AddressType.Cc, Label = "Cc" },
            new AddressTypeOption { Type = AddressType.External, Label = "Outside" }
        ]);

        MessageViewModel vm = new(MakeEntity(), mock.Object);

        Assert.Equal("OUTSIDE", vm.ExternalLabel);
    }

    /// <summary>ViewModel exposes all message fields from the entity.</summary>
    [Fact]
    public void Ctor_ExposesEntityFields()
    {
        MessageEntity entity = MakeEntity(
            messageId: "ABC123",
            body: "World",
            fromUser: "ALPHA",
            addresses:
            [
                new AddressData { UserName = "BETA", Type = "To" },
                new AddressData { UserName = "GAMMA", Type = "Cc" }
            ]);

        MessageViewModel vm = new(entity, format);

        Assert.Equal("ABC123", vm.MessageId);
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

        vm.UpdateDeliveryStatus("DEST", DestinationStatus.Received);

        Assert.Equal(DestinationStatus.Received, vm.DeliveryStatuses[0].Status);
    }

    /// <summary>UpdateDeliveryStatus recomputes OverallStatus to Confirmed when all users confirmed.</summary>
    [Fact]
    public void UpdateDeliveryStatus_AllConfirmed_OverallStatusIsConfirmed()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Sending, AddressedVia = [] },
            new() { UserName = "B", Status = DestinationStatus.Received, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("A", DestinationStatus.Received);

        Assert.Equal(DestinationStatus.Received, vm.OverallStatus);
    }

    /// <summary>Failed takes priority over Confirmed in overall status.</summary>
    [Fact]
    public void UpdateDeliveryStatus_OneFailed_OverallStatusIsFailed()
    {
        DeliveryStatus[] statuses =
        [
            new() { UserName = "A", Status = DestinationStatus.Received, AddressedVia = [] },
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
        DeliveryStatus[] statuses = [new() { UserName = "DEST", Status = DestinationStatus.Received, AddressedVia = [] }];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        Assert.Equal("RECEIVED", vm.OverallStatusText);
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
            new() { UserName = "B", Status = DestinationStatus.Received, AddressedVia = [] }
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
            new() { UserName = "B", Status = DestinationStatus.Received, AddressedVia = [] }
        ];
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: statuses), format);

        vm.UpdateDeliveryStatus("A", DestinationStatus.Read);

        Assert.Equal(DestinationStatus.Received, vm.OverallStatus);
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
        ((TestFrame)entity.Message).IsAlert = true;

        MessageViewModel vm = new(entity, format);

        Assert.True(vm.IsAlert);
    }

    /// <summary>Priority, tag and tag visibility are exposed from the stored message via IEngineController.</summary>
    [Fact]
    public void Ctor_ExposesPriorityAndTag()
    {
        MessageEntity entity = MakeEntity(tag: "URGENT");

        MessageViewModel vm = new(entity, format);

        Assert.Equal("NORMAL", vm.PriorityLabel);
        Assert.True(vm.TagsEnabled);
        Assert.Equal("URGENT", vm.Tag);
    }

    /// <summary>An empty or unrecognized message level (no message levels configured) yields a null MessageLevelColorHex rather than a fallback color.</summary>
    [Fact]
    public void Ctor_NoMessageLevel_MessageLevelColorHexIsNull()
    {
        MessageEntity entity = MakeEntity();

        MessageViewModel vm = new(entity, format);

        Assert.Equal("", vm.MessageLevelName);
        Assert.Null(vm.MessageLevelColorHex);
    }

    /// <summary>A recognized message level exposes both its name and its configured color.</summary>
    [Fact]
    public void Ctor_RecognizedMessageLevel_ExposesNameAndColor()
    {
        Mock<TestEngineController> controller = new() { CallBase = true };
        controller.Setup(c => c.MessageLevels).Returns([new MessageLevel { Name = "RESTRICTED", Color = "#C62828" }]);
        MessageEntity entity = MakeEntity(messageLevel: "RESTRICTED");

        MessageViewModel vm = new(entity, controller.Object);

        Assert.Equal("RESTRICTED", vm.MessageLevelName);
        Assert.Equal("#C62828", vm.MessageLevelColorHex);
    }

    /// <summary>DeliveryStatusRow.StatusText is the uppercase status name, and DisplayName includes addressed group context when present.</summary>
    [Fact]
    public void DeliveryStatusRow_StatusTextAndDisplayName_ReflectStatusAndGroups()
    {
        DeliveryStatusRow withoutGroups = new("USER1", DestinationStatus.Sent, []);
        Assert.Equal("SENT", withoutGroups.StatusText);
        Assert.Equal("USER1", withoutGroups.DisplayName);

        DeliveryStatusRow withGroups = new("USER1", DestinationStatus.Received, ["OPS", "ALL"]);
        Assert.Equal("RECEIVED", withGroups.StatusText);
        Assert.Equal("USER1 (OPS, ALL)", withGroups.DisplayName);
    }

    /// <summary>A status for a user written in different case still updates that user's row.</summary>
    [Fact]
    public void UpdateDeliveryStatus_DifferentCase_UpdatesRow()
    {
        DeliveryStatus status = new() { UserName = "DEST", Status = DestinationStatus.Sending, AddressedVia = [] };
        MessageViewModel vm = new(MakeEntity(deliveryStatuses: [status]), format);

        vm.UpdateDeliveryStatus("dest", DestinationStatus.Sent);

        Assert.Equal(DestinationStatus.Sent, vm.DeliveryStatuses[0].Status);
    }
}
