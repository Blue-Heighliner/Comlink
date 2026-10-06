namespace BlueHeighliner.Comlink.Tests;

/// <summary>Test <see cref="IInstallHandler"/> that recognizes only the code <c>X</c>, which installs the user <c>XUSER</c>.</summary>
public sealed class TestInstallHandler : IInstallHandler
{
    /// <inheritdoc />
    public string? Install(string code) => code == "X" ? "XUSER" : null;
}
