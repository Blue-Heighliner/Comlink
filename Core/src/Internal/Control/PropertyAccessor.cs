namespace BlueHeighliner.Comlink.Control;

/// <summary>
/// Turns a property or field access such as <c>m => m.Id</c> into the getter and setter pair the message and packet
/// mappings work with, so a host does not have to write the setter out when the field already has the right type.
/// </summary>
internal static class PropertyAccessor
{
    /// <summary>Builds a getter and a setter for the member <paramref name="property"/> reads.</summary>
    /// <typeparam name="TOwner">The type the member belongs to.</typeparam>
    /// <typeparam name="TValue">The type of the member.</typeparam>
    /// <param name="property">A plain member access (nested is fine, as in <c>m => m.Inner.Name</c>) that can also be assigned.</param>
    /// <returns>Delegates compiled once, so using them costs no more than hand-written ones.</returns>
    /// <exception cref="ArgumentException"><paramref name="property"/> is not a member access, or the member cannot be assigned.</exception>
    public static (Func<TOwner, TValue> Get, Action<TOwner, TValue> Set) Create<TOwner, TValue>(Expression<Func<TOwner, TValue>> property)
    {
        if (property.Body is not MemberExpression member)
        {
            throw new ArgumentException($"'{property}' must read a property or field of {typeof(TOwner).Name}, such as m => m.Id; use the overload that takes a setter for anything else.", nameof(property));
        }

        ParameterExpression value = Expression.Parameter(typeof(TValue), "value");
        BinaryExpression assign;
        try
        {
            assign = Expression.Assign(member, value);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"'{property}' cannot be assigned, so it cannot map a field the engine has to write; use the overload that takes a setter.", nameof(property), ex);
        }

        return (property.Compile(), Expression.Lambda<Action<TOwner, TValue>>(assign, property.Parameters[0], value).Compile());
    }
}
