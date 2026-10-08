namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test helpers that store a blank entry straight away, which the application itself no longer does.</summary>
internal static class EntryServiceTestExtensions
{
    extension(IEntryService service)
    {
        /// <summary>Creates and stores a blank draft.</summary>
        /// <returns>The stored draft.</returns>
        public async Task<DraftEntity> CreateDraft()
        {
            DraftEntity entity = await service.NewDraft();
            await service.InsertDraft(entity);
            return entity;
        }

        /// <summary>Stores a message in the Inbox the way the engine does for a received message.</summary>
        public Task<MessageEntity> StoreIncomingMessage(string messageId, string fromUser, string body, List<AddressData> addresses, DateTime sentAt, Enum? priority = null, string tag = "", string messageLevel = "", string messageAspect = "")
            => service.StoreIncomingMessage(Build(messageId, fromUser, body, addresses, sentAt, priority, tag, messageLevel, messageAspect));

        /// <summary>Stores a message in the Outbox the way the engine does for a sent one, then gives each user in <paramref name="userResults"/> the status it says.</summary>
        public async Task<MessageEntity> StoreSentMessage(string messageId, string body, List<AddressData> addresses, DateTime sentAt, IReadOnlyList<UserDeliveryResult> userResults, Enum? priority = null, string tag = "", string messageLevel = "", string messageAspect = "")
        {
            MessageEntity entity = await service.StoreSentMessage(Build(messageId, "SENDER", body, addresses, sentAt, priority, tag, messageLevel, messageAspect));
            foreach (UserDeliveryResult result in userResults)
            {
                entity = await service.UpdateDeliveryStatus(messageId, result.UserName, result.Success ? DestinationStatus.Sent : DestinationStatus.Failed) ?? entity;
            }

            return entity;
        }

        /// <summary>Creates and stores a blank note.</summary>
        /// <returns>The stored note.</returns>
        public async Task<NoteEntity> CreateNote()
        {
            NoteEntity entity = await service.NewNote();
            await service.InsertNote(entity);
            return entity;
        }
    }

    private static Message Build(string messageId, string fromUser, string body, List<AddressData> addresses, DateTime sentAt, Enum? priority, string tag, string messageLevel, string messageAspect)
        => new()
        {
            Id = messageId,
            FromUser = fromUser,
            Body = body,
            Addresses = [.. addresses.Select(a => new MessageAddress { UserName = a.UserName, Type = a.Type.ParseAddressType(), Information = a.Information })],
            SentAt = sentAt,
            Priority = priority ?? TestMessagePriority.Normal,
            Tag = tag,
            IsAlert = tag == "ALERT",
            MessageLevel = messageLevel.Length > 0 ? Enum.Parse<TestLevel>(messageLevel, ignoreCase: true) : null,
            MessageAspect = messageAspect.Length > 0 ? Enum.Parse<TestAspect>(messageAspect, ignoreCase: true) : null
        };
}
