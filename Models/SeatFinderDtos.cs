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
        var reference = referenceTime ?? DateTime.Now;

        if (IsClosedByException(reference))
        {
            return false;
        }

        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0)
        {
            return true;
        }

        if (WeeklyOpeningHours.Count == 1)
        {
            return true;
        }

        return TryGetCurrentOpeningIntervalEnd(reference, out _);
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

    public double GetRemainingOpenHours(DateTime? referenceTime = null)
    {
        var reference = referenceTime ?? DateTime.Now;

        if (IsClosedByException(reference))
        {
            return 0;
        }

        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0)
        {
            return 0;
        }

        if (WeeklyOpeningHours.Count == 1)
        {
            return 24;
        }

        if (!TryGetCurrentOpeningIntervalEnd(reference, out var intervalEnd))
        {
            return 0;
        }

        return Math.Max(0, (intervalEnd - reference).TotalHours);
    }

    public bool IsOpenUntil(DateTime referenceTime, DateTime requiredOpenUntil)
    {
        if (requiredOpenUntil <= referenceTime)
        {
            return IsCurrentlyOpen(referenceTime);
        }

        var requiredHours = (requiredOpenUntil - referenceTime).TotalHours;
        return GetRemainingOpenHours(referenceTime) >= requiredHours;
    }

    public bool TryGetNextOpeningTime(DateTime referenceTime, out DateTime nextOpeningTime)
    {
        nextOpeningTime = DateTime.MinValue;

        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0)
        {
            return false;
        }

        // "Immer offen"-Sonderfall: Nur durch Ausnahmezeiten geschlossen.
        if (WeeklyOpeningHours.Count == 1)
        {
            return TryGetCurrentClosureEnd(referenceTime, out nextOpeningTime);
        }

        var candidates = new List<DateTime>();

        foreach (var block in WeeklyOpeningHours)
        {
            if (block == null || block.Count < 2)
            {
                continue;
            }

            var start = block[0].GetParsedDate();
            if (!start.HasValue)
            {
                continue;
            }

            var startDay = start.Value.DayOfWeek;
            var startTime = start.Value.TimeOfDay;

            var daysUntilStart = ((int)startDay - (int)referenceTime.DayOfWeek + 7) % 7;
            var firstCandidate = referenceTime.Date.AddDays(daysUntilStart).Add(startTime);
            if (firstCandidate <= referenceTime)
            {
                firstCandidate = firstCandidate.AddDays(7);
            }

            // Bis zu 3 Wochen vorausschauen, um längere Ausnahme-Schließungen zu überbrücken.
            for (var weekOffset = 0; weekOffset < 3; weekOffset++)
            {
                candidates.Add(firstCandidate.AddDays(weekOffset * 7));
            }
        }

        foreach (var candidate in candidates.Distinct().OrderBy(value => value))
        {
            if (candidate <= referenceTime)
            {
                continue;
            }

            if (IsClosedByException(candidate))
            {
                continue;
            }

            nextOpeningTime = candidate;
            return true;
        }

        return false;
    }

    private bool IsClosedByException(DateTime referenceTime)
    {
        if (ExceptionalOpeningHours == null)
        {
            return false;
        }

        foreach (var exception in ExceptionalOpeningHours)
        {
            var start = exception.Start?.GetParsedDate();
            var end = exception.End?.GetParsedDate();

            if (!start.HasValue || !end.HasValue)
            {
                continue;
            }

            if (referenceTime < start.Value || referenceTime > end.Value)
            {
                continue;
            }

            if (exception.OpeningHours == null || exception.OpeningHours.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetCurrentClosureEnd(DateTime referenceTime, out DateTime closureEnd)
    {
        closureEnd = DateTime.MinValue;

        if (ExceptionalOpeningHours == null || ExceptionalOpeningHours.Count == 0)
        {
            return false;
        }

        DateTime? bestMatch = null;

        foreach (var exception in ExceptionalOpeningHours)
        {
            var start = exception.Start?.GetParsedDate();
            var end = exception.End?.GetParsedDate();

            if (!start.HasValue || !end.HasValue)
            {
                continue;
            }

            var isClosure = exception.OpeningHours == null || exception.OpeningHours.Count == 0;
            if (!isClosure)
            {
                continue;
            }

            if (referenceTime < start.Value || referenceTime > end.Value)
            {
                continue;
            }

            if (!bestMatch.HasValue || end.Value < bestMatch.Value)
            {
                bestMatch = end.Value;
            }
        }

        if (!bestMatch.HasValue)
        {
            return false;
        }

        closureEnd = bestMatch.Value;
        return true;
    }

    private bool TryGetCurrentOpeningIntervalEnd(DateTime referenceTime, out DateTime intervalEnd)
    {
        intervalEnd = DateTime.MinValue;

        if (WeeklyOpeningHours == null)
        {
            return false;
        }

        foreach (var block in WeeklyOpeningHours)
        {
            if (block == null || block.Count < 2)
            {
                continue;
            }

            var start = block[0].GetParsedDate();
            var end = block[1].GetParsedDate();

            if (!start.HasValue || !end.HasValue)
            {
                continue;
            }

            var startTime = start.Value.TimeOfDay;
            var endTime = end.Value.TimeOfDay;
            var startDay = start.Value.DayOfWeek;

            if (!TryGetContainingIntervalEnd(referenceTime, startDay, startTime, endTime, out var currentBlockEnd))
            {
                continue;
            }

            if (currentBlockEnd > intervalEnd)
            {
                intervalEnd = currentBlockEnd;
            }
        }

        return intervalEnd != DateTime.MinValue;
    }

    private static bool TryGetContainingIntervalEnd(DateTime referenceTime, DayOfWeek startDay, TimeSpan startTime, TimeSpan endTime, out DateTime intervalEnd)
    {
        intervalEnd = DateTime.MinValue;

        var daysUntilStartDay = ((int)startDay - (int)referenceTime.DayOfWeek + 7) % 7;
        var thisWeekStart = referenceTime.Date.AddDays(daysUntilStartDay).Add(startTime);

        if (IsReferenceInsideBlock(referenceTime, thisWeekStart, endTime, out intervalEnd))
        {
            return true;
        }

        var previousWeekStart = thisWeekStart.AddDays(-7);
        return IsReferenceInsideBlock(referenceTime, previousWeekStart, endTime, out intervalEnd);
    }

    private static bool IsReferenceInsideBlock(DateTime referenceTime, DateTime blockStart, TimeSpan endTime, out DateTime blockEnd)
    {
        blockEnd = blockStart.Date.Add(endTime);
        if (blockEnd <= blockStart)
        {
            blockEnd = blockEnd.AddDays(1);
        }

        return referenceTime >= blockStart && referenceTime <= blockEnd;
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
