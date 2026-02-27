using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics;
using System.IO;

namespace PlatzPilot.Models;

// DTO = Data Transfer Object. Diese Klassen existieren NUR, 
// um das chaotische JSON der API 1:1 abbilden zu können.
public static class FileLogger
{
    public static void Log(string message)
    {
        try
        {
            // Holt sich den Pfad zu deinem Windows-Desktop
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktopPath, "PlatzPilot_Log.txt");
            
            // Schreibt die Nachricht mit Uhrzeit in die Datei (erstellt sie, falls sie nicht existiert)
            File.AppendAllText(filePath, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
        }
        catch 
        { 
            // Falls Windows kurz blockiert, stürzt die App nicht ab
        }
    }
}
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
        FileLogger.Log($"[4. PARSER] ⏱️ Versuche zu parsen: '{Date}'");
        if (string.IsNullOrWhiteSpace(Date)) return null;

        if (DateTime.TryParseExact(Date, "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exactDate))
        {
            FileLogger.Log($"[4. PARSER] ✅ Perfekter Match (mit Nullen): {exactDate}");
            return exactDate;
        }

        if (DateTime.TryParse(Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
        {
            FileLogger.Log($"[4. PARSER] ✅ Fallback Match: {parsedDate}");
            return parsedDate;
        }
        
        FileLogger.Log($"[4. PARSER] ❌ KOMPLETTER FEHLSCHLAG FÜR: '{Date}'");
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

    public bool IsCurrentlyOpen()
    {
        if (WeeklyOpeningHours == null || WeeklyOpeningHours.Count == 0) return true;

        // FALL B: Die API schickt nur 1 Block für die ganze Woche (meistens 24/7 offen)
        if (WeeklyOpeningHours.Count == 1)
        {
            var block = WeeklyOpeningHours[0];
            if (block != null && block.Count >= 2)
            {
                // Wenn ein Gebäude einen Block für die ganze Woche hat, gehen wir davon aus, dass es offen ist.
                return true; 
            }
        }

        // FALL A: Die API schickt 7 einzelne Tage
        if (WeeklyOpeningHours.Count == 7)
        {
            int dayIndex = ((int)DateTime.Now.DayOfWeek + 6) % 7;
            var todaysHours = WeeklyOpeningHours[dayIndex];

            if (todaysHours == null || todaysHours.Count == 0) return false; // Heute kein Eintrag -> Geschlossen

            if (todaysHours.Count >= 2)
            {
                var start = todaysHours[0].GetParsedDate()?.TimeOfDay;
                var end = todaysHours[1].GetParsedDate()?.TimeOfDay;
                var currentTime = DateTime.Now.TimeOfDay;

                if (start.HasValue && end.HasValue)
                {
                    return currentTime >= start.Value && currentTime <= end.Value;
                }
            }
        }
        
        return true; 
    }

    public string GetTodayOpeningHoursText()
    {
        FileLogger.Log("[3. LOGIK] 🧠 Logik gestartet...");
        if (WeeklyOpeningHours == null)
        {
            FileLogger.Log("[3. LOGIK] ❌ Abbruch: WeeklyOpeningHours ist NULL.");
            return "Fehler: Liste Null";
        }
        
        FileLogger.Log($"[3. LOGIK] 📊 Anzahl der Tage/Blöcke in der Liste: {WeeklyOpeningHours.Count}");

        if (WeeklyOpeningHours.Count == 1)
        {
            FileLogger.Log("[3. LOGIK] 🔍 24/7 Block erkannt (Nur 1 Eintrag).");
            var block = WeeklyOpeningHours[0];
            if (block != null && block.Count >= 2)
            {
                var start = block[0].GetParsedDate();
                var end = block[1].GetParsedDate();
                
                if (!start.HasValue) return "Format-Fehler Start";
                if (!end.HasValue) return "Format-Fehler End";

                return $"{start.Value:HH:mm} - {end.Value:HH:mm} Uhr";
            }
            return "Block defekt";
        }

        if (WeeklyOpeningHours.Count == 7)
        {
            int dayIndex = ((int)DateTime.Now.DayOfWeek + 6) % 7;
            FileLogger.Log($"[3. LOGIK] 📅 Suche Daten für heutigen Wochentag (Index {dayIndex}).");
            
            var todaysHours = WeeklyOpeningHours[dayIndex];
            if (todaysHours == null || todaysHours.Count == 0)
            {
                FileLogger.Log("[3. LOGIK] 🛑 Heute kein Eintrag -> Geschlossen.");
                return "Geschlossen";
            }

            var start = todaysHours[0].GetParsedDate();
            var end = todaysHours[1].GetParsedDate();
            
            if (start.HasValue && end.HasValue)
            {
                FileLogger.Log($"[3. LOGIK] ✅ Zeiten berechnet: {start.Value:HH:mm} bis {end.Value:HH:mm}");
                return $"{start.Value:HH:mm} - {end.Value:HH:mm} Uhr";
            }
            FileLogger.Log("[3. LOGIK] ❌ Konnte Start oder Ende nicht parsen.");
            return "Parsing-Fehler";
        }

        FileLogger.Log($"[3. LOGIK] ❌ Unbekannte Anzahl an Wochentagen: {WeeklyOpeningHours.Count}");
        return $"Unbekannt (Tage: {WeeklyOpeningHours.Count})";
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

    // 1. Der Empfänger (Hier schlagen die Daten auf)
    [JsonPropertyName("opening_hours")]
    public JsonElement? OpeningHoursRaw 
    { 
        get => null; // Wird nicht benötigt
        set
        {
            FileLogger.Log($"\n[1. API IN] 📥 JSON-Daten empfangen. Typ: {value?.ValueKind}");
            
            // WIR ÜBERSETZEN SOFORT BEIM EINTREFFEN DER DATEN!
            if (value.HasValue && value.Value.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _openingHours = value.Value.Deserialize<OpeningHoursDto>(options);
                    FileLogger.Log($"[2. MAPPER] ✅ SOFORT umgewandelt! Anzahl Tage: {_openingHours?.WeeklyOpeningHours?.Count}");
                }
                catch (Exception ex)
                {
                    FileLogger.Log($"[2. MAPPER] 🚨 CRASH BEIM ÜBERSETZEN: {ex.Message}");
                }
            }
            else
            {
                FileLogger.Log($"[2. MAPPER] ⚠️ Ignoriert, da es kein Objekt ist.");
            }
        }
    }

    // 2. Die saubere Schnittstelle für die App
    [JsonIgnore]
    public OpeningHoursDto? OpeningHours
    {
        get
        {
            FileLogger.Log("[3. GETTER] 📞 Die UI (oder der Mapper) ruft die OpeningHours ab!");
            return _openingHours;
        }
        set 
        { 
            // Sehr wichtig, falls dein SeatFinderService die Daten manuell kopiert!
            _openingHours = value; 
        }
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
