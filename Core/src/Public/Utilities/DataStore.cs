namespace BlueHeighliner.Comlink;

/// <summary>
/// Keeps records of the host's own on the computer between runs, each under an identifier of its choosing, for any component of the host to store data it needs long term (settings, state, caches) without handling files itself.
/// A record is written as JSON, one file per identifier, in the app's data folder. Ask for it through dependency injection.
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="id"/>, replacing whatever was stored there. The record is written whole to a file of its own and put in place only once complete, so a failure while saving, or a process
    /// that reads or saves at the same time, never sees half a record: it sees the old one or the new one.
    /// </summary>
    /// <typeparam name="T">The record type, typically a record with public properties. It is written as declared here, with names as the properties are named and enums by name.</typeparam>
    /// <param name="id">The identifier to store it under. Any text but an empty one; case does not matter, so <c>Settings</c> and <c>settings</c> are the same record.</param>
    /// <param name="value">The record to store.</param>
    /// <param name="cancellation">Cancels the save. A cancelled save leaves the old record as it was.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or only white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    Task Save<T>(string id, T value, CancellationToken cancellation = default) where T : class;

    /// <summary>Returns the record stored under <paramref name="id"/>, or <see langword="null"/> when nothing is stored there.</summary>
    /// <typeparam name="T">The record type to read it as, normally the type it was saved as.</typeparam>
    /// <param name="id">The identifier it was stored under; case does not matter.</param>
    /// <param name="cancellation">Cancels the load.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or only white space.</exception>
    /// <exception cref="InvalidDataException">A record is stored under <paramref name="id"/> but cannot be read as <typeparamref name="T"/>: it is not valid JSON, or a value in it is of the wrong kind for the property it belongs to. It is left as it is, not replaced by null, so a later save does not silently lose it. Properties the type does not have are ignored and properties the file does not have keep their defaults, so a record type can gain and lose properties between runs.</exception>
    Task<T?> Load<T>(string id, CancellationToken cancellation = default) where T : class;
}
