namespace BlueHeighliner.Comlink;

/// <summary>
/// A set of pairs that can be looked up from either side: every left value belongs to exactly one right value and every right value to exactly one left value. Neither side is a key in any special sense; the names only say
/// which way a lookup goes. It is not thread safe.
/// </summary>
/// <typeparam name="TLeft">The type of the left values.</typeparam>
/// <typeparam name="TRight">The type of the right values.</typeparam>
public sealed class BiDictionary<TLeft, TRight> : IEnumerable<KeyValuePair<TLeft, TRight>> where TLeft : notnull where TRight : notnull
{
    /// <summary>Initializes an empty dictionary that compares values with their default equality.</summary>
    public BiDictionary()
        : this(null, null)
    {
    }

    /// <summary>Initializes an empty dictionary.</summary>
    /// <param name="leftComparer">Compares left values, or <see langword="null"/> for the default equality.</param>
    /// <param name="rightComparer">Compares right values, or <see langword="null"/> for the default equality.</param>
    public BiDictionary(IEqualityComparer<TLeft>? leftComparer, IEqualityComparer<TRight>? rightComparer)
    {
        rights = new(leftComparer);
        lefts = new(rightComparer);
    }

    private readonly Dictionary<TLeft, TRight> rights;
    private readonly Dictionary<TRight, TLeft> lefts;

    /// <summary>Gets how many pairs there are.</summary>
    public int Count => rights.Count;

    /// <summary>Gets every left value.</summary>
    public IReadOnlyCollection<TLeft> Lefts => rights.Keys;

    /// <summary>Gets every right value.</summary>
    public IReadOnlyCollection<TRight> Rights => lefts.Keys;

    /// <summary>Adds a pair.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <exception cref="ArgumentException"><paramref name="left"/> or <paramref name="right"/> is already in a pair.</exception>
    public void Add(TLeft left, TRight right)
    {
        if (!TryAdd(left, right))
        {
            throw new ArgumentException(rights.ContainsKey(left) ? "The left value is already in a pair." : "The right value is already in a pair.");
        }
    }

    /// <summary>Adds a pair unless <paramref name="left"/> or <paramref name="right"/> is already in one.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> if the pair was added.</returns>
    public bool TryAdd(TLeft left, TRight right)
    {
        if (rights.ContainsKey(left) || lefts.ContainsKey(right))
        {
            return false;
        }

        rights.Add(left, right);
        lefts.Add(right, left);
        return true;
    }

    /// <summary>Sets the pair of <paramref name="left"/> and <paramref name="right"/>, first removing the pair either of them was in.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    public void Set(TLeft left, TRight right)
    {
        RemoveLeft(left);
        RemoveRight(right);
        Add(left, right);
    }

    /// <summary>Returns whether <paramref name="left"/> is in a pair.</summary>
    /// <param name="left">The left value.</param>
    public bool ContainsLeft(TLeft left) => rights.ContainsKey(left);

    /// <summary>Returns whether <paramref name="right"/> is in a pair.</summary>
    /// <param name="right">The right value.</param>
    public bool ContainsRight(TRight right) => lefts.ContainsKey(right);

    /// <summary>Gets the right value paired with <paramref name="left"/>.</summary>
    /// <param name="left">The left value.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="left"/> is not in a pair.</exception>
    public TRight GetRight(TLeft left) => rights[left];

    /// <summary>Gets the left value paired with <paramref name="right"/>.</summary>
    /// <param name="right">The right value.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="right"/> is not in a pair.</exception>
    public TLeft GetLeft(TRight right) => lefts[right];

    /// <summary>Gets the right value paired with <paramref name="left"/>, if there is one.</summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value, or the default when there is no pair.</param>
    /// <returns><see langword="true"/> if <paramref name="left"/> is in a pair.</returns>
    public bool TryGetRight(TLeft left, [MaybeNullWhen(false)] out TRight right) => rights.TryGetValue(left, out right);

    /// <summary>Gets the left value paired with <paramref name="right"/>, if there is one.</summary>
    /// <param name="right">The right value.</param>
    /// <param name="left">The left value, or the default when there is no pair.</param>
    /// <returns><see langword="true"/> if <paramref name="right"/> is in a pair.</returns>
    public bool TryGetLeft(TRight right, [MaybeNullWhen(false)] out TLeft left) => lefts.TryGetValue(right, out left);

    /// <summary>Removes the pair <paramref name="left"/> is in.</summary>
    /// <param name="left">The left value.</param>
    /// <returns><see langword="true"/> if there was such a pair.</returns>
    public bool RemoveLeft(TLeft left) => rights.Remove(left, out TRight? right) && lefts.Remove(right);

    /// <summary>Removes the pair <paramref name="right"/> is in.</summary>
    /// <param name="right">The right value.</param>
    /// <returns><see langword="true"/> if there was such a pair.</returns>
    public bool RemoveRight(TRight right) => lefts.Remove(right, out TLeft? left) && rights.Remove(left);

    /// <summary>Removes every pair.</summary>
    public void Clear()
    {
        rights.Clear();
        lefts.Clear();
    }

    /// <summary>Enumerates the pairs, as left values with their right values.</summary>
    public IEnumerator<KeyValuePair<TLeft, TRight>> GetEnumerator() => rights.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
