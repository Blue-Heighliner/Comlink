namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample IUserCodeResolver that reads user info from environment variables.
/// Set USER_{CODE}_NAME, USER_{CODE}_ENV_TITLE, USER_{CODE}_ENV_COLOR to define a user.
/// </summary>
public sealed class SampleUserCodeResolver : IUserCodeResolver
{
    private static readonly Dictionary<string, UserInfo> _builtIn = new()
    {
        ["CODE1"] = new UserInfo { Name = "TEST1", Code = "CODE1", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE2"] = new UserInfo { Name = "TEST2", Code = "CODE2", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" },
        ["CODE3"] = new UserInfo { Name = "TEST3", Code = "CODE3", EnvironmentTitle = "DEV", EnvironmentColor = "#1565C0" }
    };

    /// <inheritdoc />
    public Task<UserInfo?> Resolve(string userCode, CancellationToken cancellation = default)
    {
        string upper = userCode.ToUpperInvariant();
        string prefix = $"USER_{upper}_";
        string? name = Environment.GetEnvironmentVariable($"{prefix}NAME");

        if (name is not null)
        {
            return Task.FromResult<UserInfo?>(new UserInfo
            {
                Name = name,
                Code = userCode,
                EnvironmentTitle = Environment.GetEnvironmentVariable($"{prefix}ENV_TITLE") ?? "Development",
                EnvironmentColor = Environment.GetEnvironmentVariable($"{prefix}ENV_COLOR") ?? "#1565C0"
            });
        }

        _builtIn.TryGetValue(upper, out UserInfo? builtIn);
        return Task.FromResult(builtIn);
    }
}
