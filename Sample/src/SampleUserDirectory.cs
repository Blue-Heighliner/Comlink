namespace BlueHeighliner.Comlink.Sample;

/// <summary>
/// Sample <see cref="IUserDirectory"/> that returns three hard-coded built-in user names, matching
/// <see cref="SampleUserIdentity"/>'s built-in codes. <c>config.json</c>'s <c>Users</c>/<c>UserGroups</c>
/// names are still unioned in, and endpoint/group resolution still uses the Engine default, applied
/// separately at the Engine level (see <c>Docs/Control.md</c>).
/// </summary>
public sealed class SampleUserDirectory : DefaultUserDirectory
{
    private static readonly IReadOnlyList<string> BuiltIn = ["TEST1", "TEST2", "TEST3"];

    /// <inheritdoc />
    public override Task<IReadOnlyList<string>> GetAllUserNames(CancellationToken cancellation = default) =>
        Task.FromResult(BuiltIn);
}
