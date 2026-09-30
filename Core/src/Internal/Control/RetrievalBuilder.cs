namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IRetrievalBuilder{TMessage}"/>, recording each mapping in the owning <see cref="MessageBuilder{TMessage}"/>'s field table under a <c>Retrieval.</c> name.</summary>
internal sealed class RetrievalBuilder<TMessage>(Dictionary<string, (Delegate Get, Delegate Set)> fields) : IRetrievalBuilder<TMessage> where TMessage : class, new()
{
    /// <summary>The field names every retrieval mapping must state, as they appear in the owning builder's field table and in its error message.</summary>
    public static IReadOnlyList<string> Names { get; } = [IsRequestName, FromName, ToName, AuthorsName, DestinationsName, IdsName];

    /// <summary>Field name of <see cref="IsRequest(Func{TMessage, bool}, Action{TMessage, bool})"/>.</summary>
    public const string IsRequestName = "Retrieval.IsRequest";
    /// <summary>Field name of <see cref="From(Func{TMessage, DateTime?}, Action{TMessage, DateTime?})"/>.</summary>
    public const string FromName = "Retrieval.From";
    /// <summary>Field name of <see cref="To(Func{TMessage, DateTime?}, Action{TMessage, DateTime?})"/>.</summary>
    public const string ToName = "Retrieval.To";
    /// <summary>Field name of <see cref="Authors"/>.</summary>
    public const string AuthorsName = "Retrieval.Authors";
    /// <summary>Field name of <see cref="Destinations"/>.</summary>
    public const string DestinationsName = "Retrieval.Destinations";
    /// <summary>Field name of <see cref="Ids"/>.</summary>
    public const string IdsName = "Retrieval.Ids";

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> IsRequest(Func<TMessage, bool> get, Action<TMessage, bool> set) => Map(IsRequestName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> IsRequest(Expression<Func<TMessage, bool>> property) => Map(IsRequestName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> From(Func<TMessage, DateTime?> get, Action<TMessage, DateTime?> set) => Map(FromName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> From(Expression<Func<TMessage, DateTime?>> property) => Map(FromName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> To(Func<TMessage, DateTime?> get, Action<TMessage, DateTime?> set) => Map(ToName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> To(Expression<Func<TMessage, DateTime?>> property) => Map(ToName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> Authors(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set) => MapList(AuthorsName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> Destinations(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set) => MapList(DestinationsName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TMessage> Ids(Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set) => MapList(IdsName, get, set);

    private RetrievalBuilder<TMessage> Map<T>(string name, Expression<Func<TMessage, T>> property)
    {
        (Func<TMessage, T> get, Action<TMessage, T> set) = PropertyAccessor.Create(property);
        return Map(name, get, set);
    }

    private RetrievalBuilder<TMessage> Map<T>(string name, Func<TMessage, T> get, Action<TMessage, T> set)
    {
        fields[name] = (get, set);
        return this;
    }

    private RetrievalBuilder<TMessage> MapList(string name, Func<TMessage, IEnumerable<string>> get, Action<TMessage, IReadOnlyList<string>> set)
        => Map<List<string>>(name, message => [.. get(message) ?? []], (message, values) => set(message, values));
}
