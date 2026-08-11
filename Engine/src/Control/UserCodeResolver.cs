namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Control interface: resolves a user code to its UserInfo. Returns null when the code is unrecognized.</summary>
public interface IUserCodeResolver
{
    /// <summary>Resolves <paramref name="userCode"/> to its <see cref="UserInfo"/>, or <see langword="null"/> if the code is unrecognized.</summary>
    /// <param name="userCode">The user installation code to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<UserInfo?> Resolve(string userCode, CancellationToken cancellation = default);
}

/// <summary>Stub <see cref="IUserCodeResolver"/> that only recognizes the hard-coded test code "CODE".</summary>
internal sealed class UserCodeResolver : IUserCodeResolver
{
    /// <inheritdoc />
    public Task<UserInfo?> Resolve(string userCode, CancellationToken cancellation = default)
    {
        UserInfo? result = userCode.Equals("CODE", StringComparison.OrdinalIgnoreCase)
            ? new UserInfo { Name = "TEST", Code = "CODE", EnvironmentTitle = "Test", EnvironmentColor = "#888888" }
            : null;
        return Task.FromResult(result);
    }
}
