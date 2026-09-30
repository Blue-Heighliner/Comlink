namespace BlueHeighliner.Comlink;

/// <summary>
/// Maps the fields of a retrieval request (see <see cref="UserInfo.StoresMessages"/>) onto the host's own message
/// type <typeparamref name="TMessage"/>, given to <see cref="IMessageBuilder{TMessage}.Retrieval"/>. A client asks a
/// storage server for stored messages by sending an ordinary message of the host's type with these fields set, so
/// each is a real property of the host's message, read and written with the mapped getter and setter like any other
/// field; nothing is packed into a string. Every field must be mapped.
/// </summary>
/// <typeparam name="TMessage">The host's message type.</typeparam>
public interface IRetrievalBuilder<TMessage> where TMessage : class, new()
{
    /// <summary>Maps whether the message is a retrieval request, <see langword="false"/> on every ordinary message. A request is never shown to a user as a received message.</summary>
    IRetrievalBuilder<TMessage> IsRequest(Func<TMessage, bool> get, Action<TMessage, bool> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.IsRetrieval</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TMessage> IsRequest(Expression<Func<TMessage, bool>> property);

    /// <summary>Maps the inclusive lower bound, as a UTC time, on the sent time of the messages asked for; <see langword="null"/> for no lower bound.</summary>
    IRetrievalBuilder<TMessage> From(Func<TMessage, DateTime?> get, Action<TMessage, DateTime?> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.RetrievalFrom</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TMessage> From(Expression<Func<TMessage, DateTime?>> property);

    /// <summary>Maps the inclusive upper bound, as a UTC time, on the sent time of the messages asked for; <see langword="null"/> for no upper bound.</summary>
    IRetrievalBuilder<TMessage> To(Func<TMessage, DateTime?> get, Action<TMessage, DateTime?> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.RetrievalTo</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TMessage> To(Expression<Func<TMessage, DateTime?>> property);

    /// <summary>Maps the sender user names asked for; empty for any sender. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TMessage> Authors(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set);

    /// <summary>Maps the addressee user names asked for (any address type, as written, groups unexpanded); empty for any addressee. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TMessage> Destinations(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set);

    /// <summary>Maps the message identifiers asked for; empty for any message. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TMessage> Ids(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set);
}
