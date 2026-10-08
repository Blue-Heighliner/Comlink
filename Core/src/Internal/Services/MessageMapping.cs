namespace BlueHeighliner.Comlink;

/// <summary>Turns a <see cref="Message"/>, which names the host's enum members, into the <see cref="MessageData"/> that is stored, and back.</summary>
internal static class MessageMapping
{
    extension(IEngineController engineController)
    {
        /// <summary>Returns the stored form of <paramref name="message"/>.</summary>
        /// <param name="message">The message.</param>
        /// <exception cref="ArgumentException">The message's priority, message level or message aspect is not a configured one.</exception>
        public MessageData ToData(Message message)
            => new()
            {
                Id = message.Id,
                FromUser = message.FromUser,
                Body = message.Body,
                Addresses = [.. message.Addresses.Select(address => new AddressData { UserName = address.UserName, Type = address.Type.ToString(), Information = address.Information })],
                SentAt = message.SentAt,
                Priority = engineController.StoredPriority(engineController.RequirePriority(message.Priority)),
                Tag = message.Tag,
                MessageLevel = message.MessageLevel is null ? null : engineController.MessageLevels.FirstOrDefault(level => message.MessageLevel.Equals(level.Key))?.Value ?? throw Unconfigured("message level", message.MessageLevel),
                MessageAspect = message.MessageAspect is null ? null : engineController.MessageAspects.FirstOrDefault(aspect => message.MessageAspect.Equals(aspect.Key))?.Value ?? throw Unconfigured("message aspect", message.MessageAspect),
                IsAlert = message.IsAlert
            };

        /// <summary>Returns the message <paramref name="data"/> stores. A message level or aspect that is no longer a configured one reads back as none.</summary>
        /// <param name="data">The stored message.</param>
        public Message ToMessage(MessageData data)
            => new()
            {
                Id = data.Id,
                FromUser = data.FromUser,
                Body = data.Body,
                Addresses = [.. data.Addresses.Select(address => new MessageAddress { UserName = address.UserName, Type = address.Type.ParseAddressType(), Information = address.Information })],
                SentAt = data.SentAt,
                Priority = engineController.PriorityOf(data.Priority),
                Tag = data.Tag,
                MessageLevel = data.MessageLevel is { } level ? engineController.MessageLevels.FirstOrDefault(candidate => candidate.Value == level)?.Key : null,
                MessageAspect = data.MessageAspect is { } aspect ? engineController.MessageAspects.FirstOrDefault(candidate => candidate.Value == aspect)?.Key : null,
                IsAlert = data.IsAlert
            };

        /// <summary>Returns the name of the message level <paramref name="data"/> stores, or an empty string for none or one that is no longer configured.</summary>
        /// <param name="data">The stored message.</param>
        public string NameOfLevel(MessageData data)
            => data.MessageLevel is { } level ? engineController.MessageLevels.FirstOrDefault(candidate => candidate.Value == level)?.Name ?? string.Empty : string.Empty;

        /// <summary>Returns the name of the message aspect <paramref name="data"/> stores, or an empty string for none or one that is no longer configured.</summary>
        /// <param name="data">The stored message.</param>
        public string NameOfAspect(MessageData data)
            => data.MessageAspect is { } aspect ? engineController.MessageAspects.FirstOrDefault(candidate => candidate.Value == aspect)?.Name ?? string.Empty : string.Empty;
    }

    private static ArgumentException Unconfigured(string what, Enum value) => new($"The {what} {value.GetType().Name}.{value} is used, which is not one of the configured {what}s");
}
