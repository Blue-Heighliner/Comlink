namespace BlueHeighliner.Comlink.Engine.Data.Entities;

/// <summary>A single segment within a draft body, representing either static text or a fill-in field.</summary>
public sealed class DraftBodySegmentData
{
    /// <summary>Segment kind — "text" for plain text or "fill-in" for a selectable field.</summary>
    [JsonPropertyName("kind")]    public string Kind { get; set; } = "text";
    /// <summary>Static text content when <see cref="Kind"/> is "text".</summary>
    [JsonPropertyName("text")]    public string? Text { get; set; }
    /// <summary>Identifier of the fill-in field when <see cref="Kind"/> is "fill-in".</summary>
    [JsonPropertyName("id")]      public string? FillInId { get; set; }
    /// <summary>Available choices for this fill-in field.</summary>
    [JsonPropertyName("options")] public List<string> Options { get; set; } = [];
    /// <summary>Currently selected option for this fill-in field.</summary>
    [JsonPropertyName("selected")]public string? Selected { get; set; }
}
