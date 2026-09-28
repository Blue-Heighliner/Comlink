namespace BlueHeighliner.Comlink.Peer;

/// <summary>Helpers for reading a remote node's name out of its certificate subject.</summary>
internal static class PeerIdentity
{
    /// <summary>Returns the common name (<c>CN=</c> component) of <paramref name="distinguishedName"/>, or the whole string when it has none.</summary>
    public static string ExtractCommonName(string distinguishedName)
        => ExtractCommonNames(distinguishedName) is [var first, ..] ? first : distinguishedName;

    /// <summary>Returns every common name (<c>CN=</c> component) in <paramref name="distinguishedName"/>, in order.</summary>
    public static IReadOnlyList<string> ExtractCommonNames(string distinguishedName)
    {
        List<string> names = [];
        foreach (string component in distinguishedName.Split(','))
        {
            string trimmed = component.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(trimmed["CN=".Length..]);
            }
        }

        return names;
    }
}
