namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>
/// Control interface for how this instance's own local user identity is established: a fixed debug
/// override that bypasses the normal <c>State.json</c> lookup, and resolving a user activation code
/// (entered during installation) to a <see cref="UserInfo"/>. See <see cref="Services.UserService"/>.
/// </summary>
public interface IUserIdentity
{
    /// <summary>The overridden user name for development/testing, or <see langword="null"/> if no override is active.</summary>
    string? DebugUserName { get; }
    /// <summary>Resolves <paramref name="userCode"/> to its <see cref="UserInfo"/>, or <see langword="null"/> if the code is unrecognized.</summary>
    /// <param name="userCode">The user installation code to resolve.</param>
    /// <param name="cancellation">Cancellation token.</param>
    Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default);
}

/// <summary>
/// Implements <see cref="IUserIdentity"/> with no debug override and a stub code resolver that only
/// recognizes the hard-coded test code "CODE". Describes non-config-file behavior; see
/// <see cref="ConfiguredUserIdentity"/> for how <c>config.json</c> overrides <see cref="DebugUserName"/>.
/// Members are <see langword="virtual"/> so a host can inherit and override just one — see <c>Docs/Control.md</c>.
/// </summary>
public class DefaultUserIdentity : IUserIdentity
{
    /// <inheritdoc />
    public virtual string? DebugUserName => null;

    /// <inheritdoc />
    public virtual Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default)
    {
        UserInfo? result = userCode.Equals("CODE", StringComparison.OrdinalIgnoreCase)
            ? new UserInfo { Name = "TEST", Code = "CODE", EnvironmentTitle = "Test", EnvironmentColor = "#888888" }
            : null;
        return Task.FromResult(result);
    }
}

/// <summary>
/// Engine-level decorator applying <see cref="EngineConfig.UserName"/> over whichever <see cref="IUserIdentity"/>
/// is registered (Engine default or a host override), when set — <see cref="ResolveCode"/> is left entirely
/// to the wrapped provider, since there is no corresponding <c>config.json</c> field for it. Registered by
/// <see cref="EngineExtensions.UseEngineConfigOverrides"/>, not by control-interface convention scanning.
/// </summary>
internal sealed class ConfiguredUserIdentity : IUserIdentity
{
    private readonly IUserIdentity _fallback;
    private readonly EngineConfig _config;

    /// <summary>Initializes a new instance wrapping <paramref name="fallback"/> with config overrides.</summary>
    /// <param name="fallback">The registered control-interface implementation to fall back to when config does not override.</param>
    /// <param name="config">Engine configuration providing the optional debug user name override.</param>
    public ConfiguredUserIdentity(IUserIdentity fallback, EngineConfig config)
    {
        _fallback = fallback;
        _config = config;
    }

    /// <inheritdoc />
    public string? DebugUserName => _config.UserName ?? _fallback.DebugUserName;

    /// <inheritdoc />
    public Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default) =>
        _fallback.ResolveCode(userCode, cancellation);
}
