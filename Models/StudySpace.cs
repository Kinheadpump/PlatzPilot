using Microsoft.Maui.Graphics;
using PlatzPilot.Configuration;

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
    public string LevelDisplayText => string.Format(AppText.LevelFormat, Level);
    public string RoomDisplayText => string.Format(AppText.RoomFormat, Room);
    public string ClosedStatusText
    {
        get
        {
            if (IsOpen)
            {
                return string.Empty;
            }

            var openingText = AppConfigProvider.Current.OpeningHoursText;
            if (OpeningHours?.TryGetNextOpeningTime(ReferenceTime, out var nextOpening) == true)
            {
                if (nextOpening.Date == ReferenceTime.Date)
                {
                    return string.Format(openingText.ClosedOpensTodayFormat, nextOpening);
                }

                if (nextOpening.Date == ReferenceTime.Date.AddDays(1))
                {
                    return string.Format(openingText.ClosedOpensTomorrowFormat, nextOpening);
                }

                return string.Format(openingText.ClosedOpensOnDateFormat, nextOpening);
            }

            return openingText.ClosedText;
        }
    }

    public DateTime LastUpdated { get; set; }
    public bool IsManualCount { get; set; }
    public List<SeatHistoryPoint> SeatHistory { get; set; } = [];
    public SafeArrivalRecommendation? SafeArrivalRecommendation { get; set; }

    // --- DESIGNER PROPERTIES ---
    public string AvailabilityText => string.Format(AppText.AvailabilityFormat, FreeSeats, TotalSeats);

    public double OccupancyRate => TotalSeats > 0 ? (double)OccupiedSeats / TotalSeats : 0;

    public Color OccupancyColor
    {
        get
        {
            var config = AppConfigProvider.Current.Occupancy;
            if (OccupancyRate < config.LowThreshold) return Color.FromArgb(config.LowColor);
            if (OccupancyRate < config.MediumThreshold) return Color.FromArgb(config.MediumColor);
            if (OccupancyRate < config.HighThreshold) return Color.FromArgb(config.HighColor);
            return Color.FromArgb(config.FullColor);
        }
    }
}
