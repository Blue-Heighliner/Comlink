namespace BlueHeighliner.Comlink.Sample;

/// <summary>Sample <see cref="IUserIdentity"/> that recognizes three hard-coded test codes instead of the Engine default's single one; the debug user name override uses the Engine default (<c>config.json</c>'s <c>UserName</c>, applied separately at the Engine level).</summary>
public sealed class SampleUserIdentity : DefaultUserIdentity
{
    private static readonly Dictionary<string, UserInfo> BuiltIn = new()
    {
        ["CODE1"] = new UserInfo { Name = "TEST1", Code = "CODE1", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE2"] = new UserInfo { Name = "TEST2", Code = "CODE2", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE3"] = new UserInfo { Name = "TEST3", Code = "CODE3", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" }
    };

    /// <inheritdoc />
    public override Task<UserInfo?> ResolveCode(string userCode, CancellationToken cancellation = default)
    {
        BuiltIn.TryGetValue(userCode.ToUpperInvariant(), out UserInfo? builtIn);
        return Task.FromResult(builtIn);
    }
}
