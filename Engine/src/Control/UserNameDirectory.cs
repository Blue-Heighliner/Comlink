namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: returns all known user names in the messaging system.</summary>
public interface IUserNameDirectory
{
    /// <summary>Returns all known user and group names in the messaging system.</summary>
    /// <param name="cancellation">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="IUserNameDirectory"/> returning user and group names from engine configuration.</summary>
internal sealed class UserNameDirectory : IUserNameDirectory
{
    private readonly IReadOnlyList<string> _names;

    /// <summary>Initializes a new instance loading user and group names from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing user and group definitions.</param>
    public UserNameDirectory(EngineConfig config)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (string user in config.Users.Keys)
            names.Add(user.ToUpperInvariant());
        foreach (string group in config.UserGroups.Keys)
            names.Add(group.ToUpperInvariant());
        _names = [.. names.OrderBy(n => n)];
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default) =>
        Task.FromResult(_names);
}
