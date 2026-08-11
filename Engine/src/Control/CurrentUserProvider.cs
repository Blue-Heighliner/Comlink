namespace BlueHeighliner.Comlink.Engine.Control;

/// <summary>Exposes the mutable user name of the currently running instance.</summary>
public interface ICurrentUserProvider
{
    /// <summary>The user name of the currently registered instance, or <see langword="null"/> if not yet installed.</summary>
    string? UserName { get; set; }
}

/// <summary>Tracks the user name of the currently running instance.</summary>
public sealed class CurrentUserProvider : ICurrentUserProvider
{
    /// <inheritdoc />
    public string? UserName { get; set; }
}
