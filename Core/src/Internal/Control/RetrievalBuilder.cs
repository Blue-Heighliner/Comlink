namespace BlueHeighliner.Comlink.Control;

/// <summary>Implements <see cref="IRetrievalBuilder{TFrame}"/>, recording each mapping in the owning <see cref="FrameBuilder{TFrame}"/>'s field table under a <c>Retrieval.</c> name.</summary>
internal sealed class RetrievalBuilder<TFrame>(Dictionary<string, (Delegate Get, Delegate Set)> fields) : IRetrievalBuilder<TFrame> where TFrame : class, new()
{
    /// <summary>The field names every retrieval mapping must state, as they appear in the owning builder's field table and in its error message.</summary>
    public static IReadOnlyList<string> Names { get; } = [IsRequestName, FromName, ToName, AuthorsName, DestinationsName, IdsName];

    /// <summary>Field name of <see cref="IsRequest(Func{TFrame, bool}, Action{TFrame, bool})"/>.</summary>
    public const string IsRequestName = "Retrieval.IsRequest";
    /// <summary>Field name of <see cref="From(Func{TFrame, DateTime?}, Action{TFrame, DateTime?})"/>.</summary>
    public const string FromName = "Retrieval.From";
    /// <summary>Field name of <see cref="To(Func{TFrame, DateTime?}, Action{TFrame, DateTime?})"/>.</summary>
    public const string ToName = "Retrieval.To";
    /// <summary>Field name of <see cref="Authors"/>.</summary>
    public const string AuthorsName = "Retrieval.Authors";
    /// <summary>Field name of <see cref="Destinations"/>.</summary>
    public const string DestinationsName = "Retrieval.Destinations";
    /// <summary>Field name of <see cref="Ids"/>.</summary>
    public const string IdsName = "Retrieval.Ids";

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> IsRequest(Func<TFrame, bool> get, Action<TFrame, bool> set) => Map(IsRequestName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> IsRequest(Expression<Func<TFrame, bool>> property) => Map(IsRequestName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> From(Func<TFrame, DateTime?> get, Action<TFrame, DateTime?> set) => Map(FromName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> From(Expression<Func<TFrame, DateTime?>> property) => Map(FromName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> To(Func<TFrame, DateTime?> get, Action<TFrame, DateTime?> set) => Map(ToName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> To(Expression<Func<TFrame, DateTime?>> property) => Map(ToName, property);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> Authors(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set) => MapList(AuthorsName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> Destinations(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set) => MapList(DestinationsName, get, set);

    /// <inheritdoc />
    public IRetrievalBuilder<TFrame> Ids(Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set) => MapList(IdsName, get, set);

    private RetrievalBuilder<TFrame> Map<T>(string name, Expression<Func<TFrame, T>> property)
    {
        (Func<TFrame, T> get, Action<TFrame, T> set) = PropertyAccessor.Create(property);
        return Map(name, get, set);
    }

    private RetrievalBuilder<TFrame> Map<T>(string name, Func<TFrame, T> get, Action<TFrame, T> set)
    {
        fields[name] = (get, set);
        return this;
    }

    private RetrievalBuilder<TFrame> MapList(string name, Func<TFrame, IEnumerable<string>> get, Action<TFrame, IReadOnlyList<string>> set)
        => Map<List<string>>(name, message => [.. get(message) ?? []], (message, values) => set(message, values));
}
