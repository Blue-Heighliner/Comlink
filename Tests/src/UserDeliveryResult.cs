namespace BlueHeighliner.Comlink.Tests;

/// <summary>What a test says became of a message sent to one user: the status the stored message starts that user's delivery at, see <see cref="EntryServiceTestExtensions"/>.</summary>
internal sealed class UserDeliveryResult
{
    /// <summary>Gets or sets the destination user.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the message was sent to them; otherwise it failed.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the groups that contained the user, which are not stored any more.</summary>
    public List<string> AddressedVia { get; set; } = [];
}
