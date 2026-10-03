namespace BlueHeighliner.Comlink.Sample;

/// <summary>The names of the Sample's security levels, lowest first.</summary>
public static class SampleSecurityLevels
{
    /// <summary>Gets the name of the lowest security level.</summary>
    public static string Public { get; } = "PUBLIC";

    /// <summary>Gets the name of the middle security level.</summary>
    public static string Internal { get; } = "INTERNAL";

    /// <summary>Gets the name of the highest security level.</summary>
    public static string Restricted { get; } = "RESTRICTED";
}
