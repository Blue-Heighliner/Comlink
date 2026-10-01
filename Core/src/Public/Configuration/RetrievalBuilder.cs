namespace BlueHeighliner.Comlink;

/// <summary>
/// Maps the fields of a retrieval request (see <see cref="UserInfo.StoresMessages"/>) onto the host's own frame
/// type <typeparamref name="TFrame"/>, given to <see cref="IFrameBuilder{TFrame}.Retrieval"/>. A client asks a
/// storage server for stored messages by sending an ordinary frame of the host's type with these fields set, so
/// each is a real property of the host's frame, read and written with the mapped getter and setter like any other
/// field; nothing is packed into a string. Every field must be mapped.
/// </summary>
/// <typeparam name="TFrame">The host's frame type.</typeparam>
public interface IRetrievalBuilder<TFrame> where TFrame : class, new()
{
    /// <summary>Maps whether the frame is a retrieval request, <see langword="false"/> on every ordinary frame. A request is never shown to a user as a received message.</summary>
    IRetrievalBuilder<TFrame> IsRequest(Func<TFrame, bool> get, Action<TFrame, bool> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.IsRetrieval</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TFrame> IsRequest(Expression<Func<TFrame, bool>> property);

    /// <summary>Maps the inclusive lower bound, as a UTC time, on the sent time of the messages asked for; <see langword="null"/> for no lower bound.</summary>
    IRetrievalBuilder<TFrame> From(Func<TFrame, DateTime?> get, Action<TFrame, DateTime?> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.RetrievalFrom</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TFrame> From(Expression<Func<TFrame, DateTime?>> property);

    /// <summary>Maps the inclusive upper bound, as a UTC time, on the sent time of the messages asked for; <see langword="null"/> for no upper bound.</summary>
    IRetrievalBuilder<TFrame> To(Func<TFrame, DateTime?> get, Action<TFrame, DateTime?> set);

    /// <summary>Maps the same field by the property or field the expression reads, such as <c>x => x.RetrievalTo</c>, building the setter from it. The member must have the same type and be assignable.</summary>
    IRetrievalBuilder<TFrame> To(Expression<Func<TFrame, DateTime?>> property);

    /// <summary>Maps the sender user names asked for; empty for any sender. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TFrame> Authors(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set);

    /// <summary>Maps the addressee user names asked for (any address type, as written, groups unexpanded); empty for any addressee. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TFrame> Destinations(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set);

    /// <summary>Maps the message identifiers asked for; empty for any message. The getter returns any sequence, the setter receives a list.</summary>
    IRetrievalBuilder<TFrame> Ids(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set);
}
