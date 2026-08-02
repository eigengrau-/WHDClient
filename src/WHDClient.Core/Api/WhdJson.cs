using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WHDClient.Core.Api;

/// <summary>Tolerates the several date shapes WHD emits (ISO-8601, "yyyy-MM-dd HH:mm:ss.fffzz", etc.).</summary>
public class WhdDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
{
    private static readonly string[] Formats =
    {
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd HH:mm:ss.fffzz",
        "yyyy-MM-dd HH:mm:ss.fffK",
        "yyyy-MM-dd HH:mm:sszzz",
        "yyyy-MM-dd HH:mm:ssK",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd"
    };

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return null;
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;
        foreach (var f in Formats)
        {
            if (DateTimeOffset.TryParseExact(s, f, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
                return dto;
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        else
            writer.WriteNullValue();
    }
}

/// <summary>Tolerates WHD emitting booleans as true/false, 0/1, or "true"/"false" strings.</summary>
public class WhdBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetInt32() != 0,
            JsonTokenType.String => bool.TryParse(reader.GetString(), out var b) ? b
                : int.TryParse(reader.GetString(), out var n) && n != 0,
            _ => false
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}

public static class WhdJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        o.Converters.Add(new WhdDateTimeOffsetConverter());
        o.Converters.Add(new WhdBoolConverter());
        return o;
    }
}
