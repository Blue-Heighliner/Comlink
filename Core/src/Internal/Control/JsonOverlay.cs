namespace BlueHeighliner.Comlink;

/// <summary>Lays the options a network configuration file states over a complete set of options, leaving everything it does not state as it was.</summary>
internal static class JsonOverlay
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    extension<T>(T value) where T : class
    {
        /// <summary>
        /// Returns a copy of <paramref name="value"/> with every property <paramref name="overrides"/> states replaced, found by name without regard to case and descending into nested objects. Names that are not
        /// options of the type are ignored, so a section can also carry settings that are not options.
        /// </summary>
        /// <param name="overrides">The JSON object stating the options to change.</param>
        public T Overlay(JsonElement overrides)
        {
            JsonObject merged = JsonSerializer.SerializeToNode(value, options)!.AsObject();
            Merge(merged, overrides);
            return merged.Deserialize<T>(options)!;
        }
    }

    private static void Merge(JsonObject target, JsonElement overrides)
    {
        if (overrides.ValueKind is not JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in overrides.EnumerateObject())
        {
            string key = target.Select(entry => entry.Key).FirstOrDefault(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)) ?? property.Name;
            if (property.Value.ValueKind is JsonValueKind.Object && target[key] is JsonObject nested)
            {
                Merge(nested, property.Value);
            }
            else
            {
                target[key] = JsonNode.Parse(property.Value.GetRawText());
            }
        }
    }
}
