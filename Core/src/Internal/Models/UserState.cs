namespace BlueHeighliner.Comlink;

/// <summary>Mutable snapshot of the local user's installation state read from persistent storage.</summary>
internal sealed class UserState
{
    /// <summary>Canonical user name, or <c>null</c> if not yet installed.</summary>
    public string? UserName { get; set; }

    /// <summary>Returns <c>true</c> when the user has been installed (i.e. <see cref="UserName"/> is set).</summary>
    [JsonIgnore]
    public bool IsInstalled => UserName != null;
}
