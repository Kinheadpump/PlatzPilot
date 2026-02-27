using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace PlatzPilot.Models;

public class TimestampDto
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("timezone_type")]
    public int TimezoneType { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; } 

    public DateTime? GetParsedDate()
    {
        if (string.IsNullOrWhiteSpace(Date)) return null;

        if (DateTime.TryParseExact(Date, "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exactDate))
            return exactDate;

        if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            return parsedDate;
        
        return null; 
    }
}

public class ExceptionalOpeningHoursDto
{
    [JsonPropertyName("start")]
    public TimestampDto? Start { get; set; }

    [JsonPropertyName("end")]
    public TimestampDto? End { get; set; }

    [JsonPropertyName("opening_hours")]
    public List<List<TimestampDto>>? OpeningHours { get; set; } 
}

public class OpeningHoursDto
{
    [JsonPropertyName("base_timestamp")]
    public TimestampDto? BaseTimestamp { get; set; }

    [JsonPropertyName("weekly_opening_hours")]
    public List<List<TimestampDto>>? WeeklyOpeningHours { get; set; }

    [JsonPropertyName("exceptional_opening_hours")]
    public List<ExceptionalOpeningHoursDto>? ExceptionalOpeningHours { get; set; }

    public bool IsCurrentlyOpen(DateTime? referenceTime = null)
    {
        var now = referenceTime ?? DateTime.Now;

        // 1. VORRANGSCHALTUNG: Ausnahmen prüfen (Feiertage, Ferien, Schließungen)
        if (ExceptionalOpeningHours != null)
        {
            foreach (var exception in ExceptionalOpeningHours)
            {
                var start = exception.Start?.GetParsedDate();
                var end = exception.End?.GetParsedDate();

                // Wenn wir uns JETZT in einem Ausnahme-Zeitraum befinden
                if (start.HasValue && end.HasValue && now >= start.Value && now <= end.Value)
                {
                    // Wenn das Array leer ist, ist das Gebäude in dieser Zeit komplett geschlossen
                    if (exception.OpeningHours == null || exception.OpeningHours.Count == 0)
                    {
                        return false; 
                    }
                    
                    // Wenn hier doch Zeiten stehen sollten (z.B. verkürzte Öffnungszeiten an Heiligabend), 
                    // könnte man diese hier auslesen. Da das KIT es meist für Schließungen nutzt, sind wir hier sicher.
                }
            }
        }

        // 2. NORMALE ZEITEN (Falls keine Ausnahme aktiv ist)
        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0) return true;

        if (WeeklyOpeningHours.Count == 1) return true; // 24/7 Fall

        var today = now.DayOfWeek;
        var currentTime = now.TimeOfDay;

        foreach (var block in WeeklyOpeningHours)
        {
            if (block == null || block.Count < 2) continue;

            var start = block[0].GetParsedDate();
            var end = block[1].GetParsedDate();

            if (start.HasValue && end.HasValue && start.Value.DayOfWeek == today)
            {
                if (currentTime >= start.Value.TimeOfDay && currentTime <= end.Value.TimeOfDay)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public string GetTodayOpeningHoursText(DateTime? referenceTime = null)
    {
        var now = referenceTime ?? DateTime.Now;

        // 1. VORRANGSCHALTUNG: Ausnahmen prüfen
        if (ExceptionalOpeningHours != null)
        {
            foreach (var exception in ExceptionalOpeningHours)
            {
                var start = exception.Start?.GetParsedDate();
                var end = exception.End?.GetParsedDate();

                if (start.HasValue && end.HasValue && now >= start.Value && now <= end.Value)
                {
                    if (exception.OpeningHours == null || exception.OpeningHours.Count == 0)
                    {
                        return "Geschlossen (Sonderöffnungszeiten)";
                    }
                }
            }
        }

        // 2. NORMALE ZEITEN
        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0) return "Unbekannt";

        if (WeeklyOpeningHours.Count == 1)
        {
            var block = WeeklyOpeningHours[0];
            if (block != null && block.Count >= 2)
            {
                var start = block[0].GetParsedDate();
                var end = block[1].GetParsedDate();
                if (start.HasValue && end.HasValue)
                {
                    if (start.Value.TimeOfDay == TimeSpan.Zero && end.Value.TimeOfDay.Hours >= 23)
                        return "24 Stunden geöffnet";
                    return $"{start.Value:HH:mm} - {end.Value:HH:mm} Uhr";
                }
            }
            return "Unbekannt";
        }

        var today = now.DayOfWeek;
        var todaysBlocks = new List<string>();

        foreach (var block in WeeklyOpeningHours)
        {
            if (block == null || block.Count < 2) continue;

            var start = block[0].GetParsedDate();
            var end = block[1].GetParsedDate();

            if (start.HasValue && end.HasValue && start.Value.DayOfWeek == today)
            {
                todaysBlocks.Add($"{start.Value:HH:mm} - {end.Value:HH:mm}");
            }
        }

        if (todaysBlocks.Count == 0) return "Geschlossen";

        return string.Join(", ", todaysBlocks) + " Uhr";
    }
}

public class SeatRecordDto
{
    [JsonPropertyName("timestamp")]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName("location_name")]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName("occupied_seats")]
    public int OccupiedSeats { get; set; }

    [JsonPropertyName("free_seats")]
    public int FreeSeats { get; set; }
}

public class LocationMetadataDto
{
    [JsonPropertyName("timestamp")]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("long_name")]
    public string LongName { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("building")]
    public string? Building { get; set; }

    [JsonPropertyName("level")]
    public string? Level { get; set; }

    [JsonPropertyName("room")]
    public string? Room { get; set; }

    [JsonPropertyName("geo_coordinates")]
    public string? GeoCoordinates { get; set; }

    [JsonPropertyName("available_seats")]
    public int AvailableSeats { get; set; }

   // Der interne Speicher für die fertigen Daten
    private OpeningHoursDto? _openingHours;

    [JsonPropertyName("opening_hours")]
    public JsonElement? OpeningHoursRaw 
    { 
        get => null; 
        set
        {
            if (value.HasValue && value.Value.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _openingHours = value.Value.Deserialize<OpeningHoursDto>(options);
                }
                catch 
                {
                    _openingHours = null; // Stilles Fehlschlagen, Fallback greift
                }
            }
        }
    }

    [JsonIgnore]
    public OpeningHoursDto? OpeningHours
    {
        get => _openingHours;
        set => _openingHours = value; 
    }

    [JsonPropertyName("super_location")]
    public string? SuperLocation { get; set; }

    [JsonPropertyName("sub_locations")]
    public List<string>? SubLocations { get; set; }
}

public class SeatFinderResponseDto
{
    [JsonPropertyName("seatestimate")]
    public Dictionary<string, List<SeatRecordDto>>? SeatEstimates { get; set; }

    [JsonPropertyName("manualcount")]
    public Dictionary<string, List<SeatRecordDto>>? ManualCounts { get; set; }

    [JsonPropertyName("location")]
    public Dictionary<string, List<LocationMetadataDto>>? Locations { get; set; }
}
