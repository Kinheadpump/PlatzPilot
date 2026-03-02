using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlatzPilot.Configuration;

public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    private static readonly string[] Formats =
    [
        "c",
        "g",
        "hh\\:mm",
        "hh\\:mm\\:ss"
    ];

    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected TimeSpan string.");
        }

        var raw = reader.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return TimeSpan.Zero;
        }

        if (TimeSpan.TryParseExact(raw, Formats, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        throw new JsonException($"Invalid TimeSpan value: {raw}");
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture));
    }
}
