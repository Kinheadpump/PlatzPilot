using System.Globalization;
using System.Text.Json.Serialization;
using PlatzPilot.Configuration;

namespace PlatzPilot.Models;

public class TimestampDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Date)]
    public string? Date { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.TimezoneType)]
    public int TimezoneType { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Timezone)]
    public string? Timezone { get; set; }

    public DateTime? GetParsedDate()
    {
        if (string.IsNullOrWhiteSpace(Date))
        {
            return null;
        }

        var timestampFormat = AppConfigProvider.Current.SeatFinder.TimestampFormat;
        if (!string.IsNullOrWhiteSpace(timestampFormat)
            && DateTime.TryParseExact(Date, timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exactDate))
        {
            return exactDate;
        }

        if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
        {
            return parsedDate;
        }

        return null;
    }
}