namespace BlueHeighliner.Comlink;

/// <summary>Turns a <see cref="Message"/>, which names the host's enum members, into the <see cref="MessageData"/> that is stored, and back.</summary>
internal static class MessageMapping
{
    extension(IEngineController engineController)
    {
        /// <summary>Returns the stored form of <paramref name="message"/>.</summary>
        /// <param name="message">The message.</param>
        /// <exception cref="ArgumentException">The message's priority, level or aspect is not a configured one.</exception>
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
                Level = message.Level is null ? null : engineController.Levels.FirstOrDefault(level => message.Level.Equals(level.Key))?.Value ?? throw Unconfigured("level", message.Level),
                Aspect = message.Aspect is null ? null : engineController.Aspects.FirstOrDefault(aspect => message.Aspect.Equals(aspect.Key))?.Value ?? throw Unconfigured("aspect", message.Aspect),
                IsAlert = message.IsAlert
            };

        /// <summary>Returns the message <paramref name="data"/> stores. A level or aspect that is no longer a configured one reads back as none.</summary>
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
                Level = engineController.LevelOf(data),
                Aspect = engineController.AspectOf(data),
                IsAlert = data.IsAlert
            };

        /// <summary>Returns the level <paramref name="data"/> stores, or <see langword="null"/> for none or one that is no longer configured.</summary>
        /// <param name="data">The stored message.</param>
        public Enum? LevelOf(MessageData data) => engineController.Levels.Find(data.Level)?.Key;

        /// <summary>Returns the aspect <paramref name="data"/> stores, or <see langword="null"/> for none or one that is no longer configured.</summary>
        /// <param name="data">The stored message.</param>
        public Enum? AspectOf(MessageData data) => data.Aspect is { } aspect ? engineController.Aspects.FirstOrDefault(candidate => candidate.Value == aspect)?.Key : null;
    }

    private static ArgumentException Unconfigured(string what, Enum value) => new($"The {what} {value.GetType().Name}.{value} is used, which is not one of the configured {what}s");
}
