namespace Catalog.Api.Json;

public sealed class TrimmingStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType is JsonTokenType.Null ? null : reader.GetString()?.Trim();

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
