namespace BlueHeighliner.Comlink.Sample;

/// <summary>The names of the Sample's message priorities; the first three are chosen by users and the last two are only assigned by the handlers.</summary>
public static class SamplePriorities
{
    /// <summary>Gets the name of the lowest user priority.</summary>
    public static string Low { get; } = "LOW";

    /// <summary>Gets the name of the middle user priority.</summary>
    public static string Medium { get; } = "MEDIUM";

    /// <summary>Gets the name of the highest user priority.</summary>
    public static string High { get; } = "HIGH";

    /// <summary>Gets the name of the system priority retrieval requests are sent with.</summary>
    public static string Retrieval { get; } = "RETRIEVAL";

    /// <summary>Gets the name of the system priority read and receive receipts are sent with.</summary>
    public static string Receipt { get; } = "RECEIPT";
}
