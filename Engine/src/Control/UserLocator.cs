namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: resolves a user name to its TCP peer endpoint for P2P delivery.</summary>
public interface IUserLocator
{
    /// <summary>Returns the TCP endpoint for <paramref name="userName"/>, or <see langword="null"/> if the user is unknown.</summary>
    /// <param name="userName">The user name to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default);
}

/// <summary>Implements <see cref="IUserLocator"/> resolving endpoints from user definitions in engine configuration.</summary>
internal sealed class UserLocator : IUserLocator
{
    private readonly IReadOnlyDictionary<string, UserEndpoint> _endpoints;

    /// <summary>Initializes a new instance loading user endpoints from the given engine configuration.</summary>
    /// <param name="config">Engine configuration providing user endpoint definitions.</param>
    public UserLocator(EngineConfig config) => _endpoints = config.GetUserEndpoints();

    /// <inheritdoc />
    public Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default)
    {
        _endpoints.TryGetValue(userName, out UserEndpoint? endpoint);
        return Task.FromResult(endpoint);
    }
}
