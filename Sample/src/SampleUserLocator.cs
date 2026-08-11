namespace BlueHeighliner.Comlink.Sample;

/// <summary>Sample <see cref="IUserLocator"/> that checks config overrides first, then falls back to <c>PEER_{USERNAME}=ip:port</c> environment variables.</summary>
internal sealed class SampleUserLocator : IUserLocator
{
    private readonly IReadOnlyDictionary<string, UserEndpoint> _overrides;

    /// <summary>Initializes a new <see cref="SampleUserLocator"/> using user definitions from the given config.</summary>
    /// <param name="config">Engine configuration providing user endpoint overrides and definitions.</param>
    public SampleUserLocator(EngineConfig config)
        => _overrides = config.GetUserEndpoints();

    /// <inheritdoc />
    public Task<UserEndpoint?> GetEndpoint(string userName, CancellationToken cancellation = default)
    {
        if (_overrides.TryGetValue(userName, out UserEndpoint? endpoint))
            return Task.FromResult<UserEndpoint?>(endpoint);

        string envKey = $"PEER_{userName.ToUpperInvariant().Replace("-", "_")}";
        string? value = Environment.GetEnvironmentVariable(envKey);

        if (value is null) return Task.FromResult<UserEndpoint?>(null);

        string[] parts = value.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            return Task.FromResult<UserEndpoint?>(null);

        return Task.FromResult<UserEndpoint?>(new UserEndpoint { IpAddress = parts[0], Port = port });
    }
}
