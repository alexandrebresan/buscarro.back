using System.Text.Json.Serialization;
using System.Text.Json;
using System.Globalization;

namespace Buscarro.Infrastructure.Contracts;

public sealed class CarzinClassifiedSearchResponseContract
{
    [JsonPropertyName("current_results")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? CurrentResults { get; set; }

    [JsonPropertyName("total_results")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? TotalResults { get; set; }

    [JsonPropertyName("current_page")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? CurrentPage { get; set; }

    [JsonPropertyName("total_pages")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? TotalPages { get; set; }

    [JsonPropertyName("results")]
    public List<CarzinClassifiedContract> Results { get; set; } = [];
}

public sealed class CarzinClassifiedContract
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("manufacturer_id")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? ManufacturerId { get; set; }

    [JsonPropertyName("model_id")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? ModelId { get; set; }

    [JsonPropertyName("version_id")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? VersionId { get; set; }

    [JsonPropertyName("year_manufacture")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? YearManufacture { get; set; }

    [JsonPropertyName("year_model")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? YearModel { get; set; }

    [JsonPropertyName("price")]
    [JsonConverter(typeof(FlexibleNullableDecimalJsonConverter))]
    public decimal? Price { get; set; }

    [JsonPropertyName("mileage")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? Mileage { get; set; }

    [JsonPropertyName("doors")]
    [JsonConverter(typeof(FlexibleNullableIntJsonConverter))]
    public int? Doors { get; set; }

    [JsonPropertyName("armored")]
    public JsonElement Armored { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("fuel")]
    public string? Fuel { get; set; }

    [JsonPropertyName("gear")]
    public string? Gear { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal sealed class FlexibleNullableIntJsonConverter : JsonConverter<int?>
{
    public override int? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetInt32(out var value) => value,
            JsonTokenType.String => ReadString(reader.GetString()),
            _ => null
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        int? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }

    private static int? ReadString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
            ? intValue
            : null;
    }
}

internal sealed class FlexibleNullableDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number when reader.TryGetDecimal(out var value) => value,
            JsonTokenType.String => ReadString(reader.GetString()),
            _ => null
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        decimal? value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }

    private static decimal? ReadString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)
                ? decimalValue
                : null;
    }
}
