namespace BlueHeighliner.Comlink;

/// <summary>Exposes the user name of the currently running instance, worked out from its certificate when it started.</summary>
internal interface ICurrentUserProvider
{
    /// <summary>The user name of the currently running instance, or <see langword="null"/> only where no identity was given, which is never the case in a running engine.</summary>
    string? UserName { get; set; }
}

/// <summary>Tracks the user name of the currently running instance.</summary>
internal sealed class CurrentUserProvider : ICurrentUserProvider
{
    /// <inheritdoc />
    public string? UserName { get; set; }
}
