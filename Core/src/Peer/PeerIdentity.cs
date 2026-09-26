namespace BlueHeighliner.Comlink.Peer;

/// <summary>Helpers for reading a remote node's name out of its certificate subject.</summary>
internal static class PeerIdentity
{
    /// <summary>Returns the common name (<c>CN=</c> component) of <paramref name="distinguishedName"/>, or the whole string when it has none.</summary>
    public static string ExtractCommonName(string distinguishedName)
    {
        foreach (string component in distinguishedName.Split(','))
        {
            string trimmed = component.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["CN=".Length..];
            }
        }

        return distinguishedName;
    }
}
