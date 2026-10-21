namespace BlueHeighliner.Comlink;

/// <summary>Reads the identifier of an activity log event, which an export wrote as a number before identifiers were text; a number is taken as the number of one of the engine's own events.</summary>
internal sealed class EventIdJsonConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.Number => LogEvents.FromNumber(reader.GetInt32()),
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            _ => string.Empty
        };

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
