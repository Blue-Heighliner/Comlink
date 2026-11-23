namespace BlueHeighliner.Comlink;

/// <summary>Extensions for values that may be null.</summary>
public static class NullExtensions
{
    extension<T>(T value)
    {
        /// <summary>Returns the value, or throws when it is <see langword="null"/>, so a nullable value can be used where a non-null one is needed.</summary>
        /// <param name="message">What the exception says, or <see langword="null"/> for a message naming the type.</param>
        /// <returns>The value, known not to be <see langword="null"/>.</returns>
        /// <exception cref="InvalidOperationException">The value is <see langword="null"/>.</exception>
        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public T NotNull(string? message = null)
            => value ?? throw new InvalidOperationException(message ?? $"{typeof(T).Name} is null");
    }
}
