using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;
using PlatzPilot.Configuration;

namespace PlatzPilot.Models;

internal static class SeatFinderJsonKeys
{
    public const string Date = "date";
    public const string TimezoneType = "timezone_type";
    public const string Timezone = "timezone";
    public const string Start = "start";
    public const string End = "end";
    public const string OpeningHours = "opening_hours";
    public const string BaseTimestamp = "base_timestamp";
    public const string WeeklyOpeningHours = "weekly_opening_hours";
    public const string ExceptionalOpeningHours = "exceptional_opening_hours";
    public const string Timestamp = "timestamp";
    public const string LocationName = "location_name";
    public const string OccupiedSeats = "occupied_seats";
    public const string FreeSeats = "free_seats";
    public const string Name = "name";
    public const string LongName = "long_name";
    public const string Url = "url";
    public const string Building = "building";
    public const string Level = "level";
    public const string Room = "room";
    public const string GeoCoordinates = "geo_coordinates";
    public const string AvailableSeats = "available_seats";
    public const string SuperLocation = "super_location";
    public const string SubLocations = "sub_locations";
    public const string SeatEstimate = "seatestimate";
    public const string ManualCount = "manualcount";
    public const string Location = "location";
}

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
        if (string.IsNullOrWhiteSpace(Date)) return null;

        var timestampFormat = AppConfigProvider.Current.SeatFinder.TimestampFormat;
        if (!string.IsNullOrWhiteSpace(timestampFormat) &&
            DateTime.TryParseExact(Date, timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exactDate))
        {
            return exactDate;
        }

        if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            return parsedDate;
        
        return null; 
    }
}

public class ExceptionalOpeningHoursDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Start)]
    public TimestampDto? Start { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.End)]
    public TimestampDto? End { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.OpeningHours)]
    public List<List<TimestampDto>>? OpeningHours { get; set; } 
}

public class OpeningHoursDto
{
    [JsonPropertyName(SeatFinderJsonKeys.BaseTimestamp)]
    public TimestampDto? BaseTimestamp { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.WeeklyOpeningHours)]
    public List<List<TimestampDto>>? WeeklyOpeningHours { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.ExceptionalOpeningHours)]
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
        var textConfig = AppConfigProvider.Current.OpeningHoursText;

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
                        return textConfig.ClosedSpecialText;
                    }
                }
            }
        }

        // 2. NORMALE ZEITEN
        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0) return textConfig.UnknownText;

        if (WeeklyOpeningHours.Count == 1)
        {
            var block = WeeklyOpeningHours[0];
            if (block != null && block.Count >= 2)
            {
                var start = block[0].GetParsedDate();
                var end = block[1].GetParsedDate();
                if (start.HasValue && end.HasValue)
                {
                    var settings = AppConfigProvider.Current.OpeningHours;
                    if (start.Value.TimeOfDay == TimeSpan.Zero && end.Value.TimeOfDay.Hours >= settings.AlwaysOpenHourThreshold)
                        return textConfig.AlwaysOpenText;
                    var timeRange = string.Format(
                        CultureInfo.CurrentCulture,
                        textConfig.TimeRangeFormat,
                        start.Value,
                        end.Value);
                    return timeRange + textConfig.HoursSuffix;
                }
            }
            return textConfig.UnknownText;
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
                var timeRange = string.Format(
                    CultureInfo.CurrentCulture,
                    textConfig.TimeRangeFormat,
                    start.Value,
                    end.Value);
                todaysBlocks.Add(timeRange);
            }
        }

        if (todaysBlocks.Count == 0) return textConfig.ClosedText;

        return string.Join(textConfig.TimeRangeSeparator, todaysBlocks) + textConfig.HoursSuffix;
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
            return AppConfigProvider.Current.OpeningHours.FullDayHours;
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

            var daysPerWeek = Math.Max(1, AppConfigProvider.Current.OpeningHours.DaysPerWeek);
            var daysUntilStart = ((int)startDay - (int)referenceTime.DayOfWeek + daysPerWeek) % daysPerWeek;
            var firstCandidate = referenceTime.Date.AddDays(daysUntilStart).Add(startTime);
            if (firstCandidate <= referenceTime)
            {
                firstCandidate = firstCandidate.AddDays(daysPerWeek);
            }

            // Bis zu 3 Wochen vorausschauen, um längere Ausnahme-Schließungen zu überbrücken.
            var maxWeeks = Math.Max(1, AppConfigProvider.Current.OpeningHours.MaxNextOpeningWeeks);
            for (var weekOffset = 0; weekOffset < maxWeeks; weekOffset++)
            {
                candidates.Add(firstCandidate.AddDays(weekOffset * daysPerWeek));
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

        var daysPerWeek = Math.Max(1, AppConfigProvider.Current.OpeningHours.DaysPerWeek);
        var daysUntilStartDay = ((int)startDay - (int)referenceTime.DayOfWeek + daysPerWeek) % daysPerWeek;
        var thisWeekStart = referenceTime.Date.AddDays(daysUntilStartDay).Add(startTime);

        if (IsReferenceInsideBlock(referenceTime, thisWeekStart, endTime, out intervalEnd))
        {
            return true;
        }

        var previousWeekStart = thisWeekStart.AddDays(-daysPerWeek);
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
    [JsonPropertyName(SeatFinderJsonKeys.Timestamp)]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.LocationName)]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.OccupiedSeats)]
    public int OccupiedSeats { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.FreeSeats)]
    public int FreeSeats { get; set; }
}

public class LocationMetadataDto
{
    [JsonPropertyName(SeatFinderJsonKeys.Timestamp)]
    public TimestampDto? Timestamp { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Name)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.LongName)]
    public string LongName { get; set; } = string.Empty;

    [JsonPropertyName(SeatFinderJsonKeys.Url)]
    public string? Url { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Building)]
    public string? Building { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Level)]
    public string? Level { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Room)]
    public string? Room { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.GeoCoordinates)]
    public string? GeoCoordinates { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.AvailableSeats)]
    public int AvailableSeats { get; set; }

   // Der interne Speicher für die fertigen Daten
    private OpeningHoursDto? _openingHours;

    [JsonPropertyName(SeatFinderJsonKeys.OpeningHours)]
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

    [JsonPropertyName(SeatFinderJsonKeys.SuperLocation)]
    public string? SuperLocation { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.SubLocations)]
    public List<string>? SubLocations { get; set; }
}

public class SeatFinderResponseDto
{
    [JsonPropertyName(SeatFinderJsonKeys.SeatEstimate)]
    public Dictionary<string, List<SeatRecordDto>>? SeatEstimates { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.ManualCount)]
    public Dictionary<string, List<SeatRecordDto>>? ManualCounts { get; set; }

    [JsonPropertyName(SeatFinderJsonKeys.Location)]
    public Dictionary<string, List<LocationMetadataDto>>? Locations { get; set; }
}
