using Microsoft.Maui.Graphics;

namespace PlatzPilot.Models;

public class StudySpace
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    public int TotalSeats { get; set; }
    public int OccupiedSeats { get; set; }
    public int FreeSeats { get; set; }
    
    public string? Building { get; set; }
    public string? Level { get; set; }
    
    // ---> HIER WIEDER EINGEFÜGT <---
    public string? Room { get; set; } 
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Url { get; set; }
    public string? SuperLocation { get; set; }
    public List<string> SubLocations { get; set; } = new();

    public OpeningHoursDto? OpeningHours { get; set; }
    public DateTime ReferenceTime { get; set; } = DateTime.Now;
    public bool IsOpen => OpeningHours?.IsCurrentlyOpen(ReferenceTime) ?? true;
    public bool HasLevel => !string.IsNullOrWhiteSpace(Level);
    public bool HasRoom => !string.IsNullOrWhiteSpace(Room);
    public string LevelDisplayText => $"Ebene {Level}";
    public string RoomDisplayText => $"Raum {Room}";
    public string ClosedStatusText
    {
        get
        {
            if (IsOpen)
            {
                return string.Empty;
            }

            if (OpeningHours?.TryGetNextOpeningTime(ReferenceTime, out var nextOpening) == true)
            {
                if (nextOpening.Date == ReferenceTime.Date)
                {
                    return $"Geschlossen - öffnet heute um {nextOpening:HH:mm} Uhr";
                }

                if (nextOpening.Date == ReferenceTime.Date.AddDays(1))
                {
                    return $"Geschlossen - öffnet morgen um {nextOpening:HH:mm} Uhr";
                }

                return $"Geschlossen - öffnet am {nextOpening:dd.MM.} um {nextOpening:HH:mm} Uhr";
            }

            return "Geschlossen";
        }
    }

    public DateTime LastUpdated { get; set; }
    public bool IsManualCount { get; set; }
    public List<SeatHistoryPoint> SeatHistory { get; set; } = [];
    public SafeArrivalRecommendation? SafeArrivalRecommendation { get; set; }

    // --- DESIGNER PROPERTIES ---
    public string AvailabilityText => $"{FreeSeats} von {TotalSeats}";

    public double OccupancyRate => TotalSeats > 0 ? (double)OccupiedSeats / TotalSeats : 0;

    public Color OccupancyColor
    {
        get
        {
            if (OccupancyRate < 0.4) return Color.FromArgb("#2ecc71"); // Grün
            if (OccupancyRate < 0.7) return Color.FromArgb("#f1c40f"); // Gelb
            if (OccupancyRate < 0.9) return Color.FromArgb("#e67e22"); // Orange
            return Color.FromArgb("#e74c3c"); // Rot
        }
    }
}
