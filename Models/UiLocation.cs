using CommunityToolkit.Mvvm.ComponentModel;

namespace PlatzPilot.Models;

// WICHTIG: Erbt jetzt von ObservableObject und ist partial!
public partial class UiLocation : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int FreeSeats { get; set; }
    public int OccupiedSeats { get; set; }
    public bool IsManualCount { get; set; }
    public List<StudySpace> SubSpaces { get; set; } = new();
    public string? MainUrl => SubSpaces.FirstOrDefault()?.Url;
    public string? BuildingNumber { get; set; }
    public DateTime ReferenceTime { get; set; } = DateTime.Now;

    public DateTime? LastUpdated => SubSpaces.Max(s => s.LastUpdated);
    
    public string LastUpdatedText => LastUpdated.HasValue && LastUpdated.Value.Year > 2000 
        ? $"Stand: {LastUpdated.Value:dd.MM.yy HH:mm} Uhr" 
        : "Stand: Unbekannt";

    public double Latitude => SubSpaces.FirstOrDefault()?.Latitude ?? 0;
    public double Longitude => SubSpaces.FirstOrDefault()?.Longitude ?? 0;
    
    // Prüft, ob wir gültige Koordinaten für den Maps-Button haben
    public bool HasLocation => Latitude != 0 && Longitude != 0;

    // Prüft, ob der erste Raum (und damit das Gebäude) offen ist
    public bool IsOpen => SubSpaces.FirstOrDefault()?.OpeningHours?.IsCurrentlyOpen(ReferenceTime) ?? true;

    // Holt den Text für die Detailseite
    public string TodayOpeningHours 
    {
        get
        {
            var firstSpace = SubSpaces.FirstOrDefault();
            
            if (firstSpace == null)
            {
                return "Keine Räume";
            }

            if (firstSpace.OpeningHours == null)
            {
                return "Unbekannt (Objekt Null)";
            }

            var text = firstSpace.OpeningHours.GetTodayOpeningHoursText(ReferenceTime);
            return text;
        }
    }
    public double OccupancyRate => TotalSeats > 0 ? (double)OccupiedSeats / TotalSeats : 0;
    public string AvailabilityText => $"{FreeSeats} von {TotalSeats}";

    public Color OccupancyColor
    {
        get
        {
            if (OccupancyRate < 0.4) return Color.FromArgb("#2ecc71");
            if (OccupancyRate < 0.7) return Color.FromArgb("#f1c40f");
            if (OccupancyRate < 0.9) return Color.FromArgb("#e67e22");
            return Color.FromArgb("#e74c3c");
        }
    }

    // --- NEU: Favoriten-Logik ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
    [NotifyPropertyChangedFor(nameof(FavoriteColor))]
    private bool _isFavorite;

    // Diese Properties steuern das Aussehen des Sterns in der UI automatisch!
    public string FavoriteIcon => IsFavorite ? "★" : "☆";
    public Color FavoriteColor => IsFavorite ? Color.FromArgb("#f1c40f") : Colors.Gray;
}
