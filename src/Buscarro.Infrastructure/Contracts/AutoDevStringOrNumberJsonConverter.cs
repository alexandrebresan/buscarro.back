using System.Text.Json;
using System.Text.Json.Serialization;

namespace Buscarro.Infrastructure.Contracts;

internal sealed class AutoDevStringOrNumberJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return reader.GetString();
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number)
        {
            using var value = JsonDocument.ParseValue(ref reader);
            return value.RootElement.GetRawText();
        }
        throw new JsonException("AutoDev value must be a string, number, or null.");
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
