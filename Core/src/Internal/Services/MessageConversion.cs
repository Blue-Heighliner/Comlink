namespace BlueHeighliner.Comlink;

/// <summary>Turns the engine's own <see cref="Message"/> into the host's typed <see cref="Message{TPriority, TLevel, TAspect}"/> and back.</summary>
internal static class MessageConversion
{
    extension(Message message)
    {
        /// <summary>Returns the message typed by the host's enums.</summary>
        /// <typeparam name="TPriority">The enum the host stated for its priorities.</typeparam>
        /// <typeparam name="TLevel">The enum the host stated for its message levels.</typeparam>
        /// <typeparam name="TAspect">The enum the host stated for its message aspects.</typeparam>
        public Message<TPriority, TLevel, TAspect> ToTyped<TPriority, TLevel, TAspect>() where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
            => new()
            {
                Id = message.Id,
                FromUser = message.FromUser,
                Body = message.Body,
                Addresses = message.Addresses,
                SentAt = message.SentAt,
                Priority = (TPriority)message.Priority,
                Tag = message.Tag,
                MessageLevel = message.MessageLevel is { } level ? (TLevel)level : null,
                MessageAspect = message.MessageAspect is { } aspect ? (TAspect)aspect : null,
                IsAlert = message.IsAlert
            };
    }

    extension<TPriority, TLevel, TAspect>(Message<TPriority, TLevel, TAspect> message) where TPriority : struct, Enum where TLevel : struct, Enum where TAspect : struct, Enum
    {
        /// <summary>Returns the message as the engine holds it.</summary>
        public Message ToUntyped()
            => new()
            {
                Id = message.Id,
                FromUser = message.FromUser,
                Body = message.Body,
                Addresses = message.Addresses,
                SentAt = message.SentAt,
                Priority = message.Priority,
                Tag = message.Tag,
                MessageLevel = message.MessageLevel,
                MessageAspect = message.MessageAspect,
                IsAlert = message.IsAlert
            };
    }
}
