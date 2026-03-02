using CommunityToolkit.Mvvm.ComponentModel;
using PlatzPilot.Configuration;

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
    public string BuildingDisplayText => string.IsNullOrWhiteSpace(BuildingNumber)
        ? AppText.BuildingUnknownText
        : BuildingNumber;
    public DateTime ReferenceTime { get; set; } = DateTime.Now;
    public string BestArrivalText { get; set; } = AppText.RecommendationNoneText;
    public bool HasArrivalInsights { get; set; }
    public string PeakAverageText { get; set; } = AppText.PeakNoneText;
    public string SafetyLevelText { get; set; } = string.Format(AppText.QualityFormat, AppText.QualityLow);
    public string PeakTrendText { get; set; } = string.Format(AppText.PeakTrendFormat, AppText.PeakTrendFlat);

    public DateTime? LastUpdated => SubSpaces.Max(s => s.LastUpdated);
    
    public string LastUpdatedText => LastUpdated.HasValue &&
                                     LastUpdated.Value.Year > AppConfigProvider.Current.UiNumbers.UnknownYearThreshold
        ? string.Format(AppText.LastUpdatedFormat, LastUpdated.Value)
        : AppText.LastUpdatedUnknownText;

    public double Latitude => SubSpaces.FirstOrDefault()?.Latitude ?? 0;
    public double Longitude => SubSpaces.FirstOrDefault()?.Longitude ?? 0;
    
    // Prüft, ob wir gültige Koordinaten für den Maps-Button haben
    public bool HasLocation => Latitude != 0 && Longitude != 0;

    // Prüft, ob der erste Raum (und damit das Gebäude) offen ist
    public bool IsOpen => SubSpaces.FirstOrDefault()?.OpeningHours?.IsCurrentlyOpen(ReferenceTime) ?? true;

    public bool IsStudentOnlyClosed => !IsOpen && IsStudentAccessLocation && IsWithinStudentAccessHours;
    public string ClosedStatusText => IsStudentOnlyClosed ? AppText.ClosedStudentsLabel : AppText.ClosedLabel;
    public bool ShowBestArrivalInTile => IsOpen || IsStudentOnlyClosed;
    public Microsoft.Maui.Thickness ClosedLabelMargin => IsStudentOnlyClosed
        ? new Microsoft.Maui.Thickness(0, AppConfigProvider.Current.UiNumbers.StudentClosedLabelTopMargin, 0, 0)
        : new Microsoft.Maui.Thickness(0);

    // Holt den Text für die Detailseite
    public string TodayOpeningHours 
    {
        get
        {
            var firstSpace = SubSpaces.FirstOrDefault();
            var openingText = AppConfigProvider.Current.OpeningHoursText;
            
            if (firstSpace == null)
            {
                return openingText.NoRoomsText;
            }

            if (firstSpace.OpeningHours == null)
            {
                return openingText.UnknownObjectText;
            }

            var text = firstSpace.OpeningHours.GetTodayOpeningHoursText(ReferenceTime);
            if (text.StartsWith(openingText.ClosedText, StringComparison.OrdinalIgnoreCase) &&
                firstSpace.OpeningHours.TryGetNextOpeningTime(ReferenceTime, out var nextOpening))
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

            return text;
        }
    }
    public double OccupancyRate => TotalSeats > 0 ? (double)OccupiedSeats / TotalSeats : 0;
    public string AvailabilityText => string.Format(AppText.AvailabilityFormat, FreeSeats, TotalSeats);

    public string HomeAvailabilityText => IsOpen ? FreeSeats.ToString() : AppText.NoCurrentInfoText;
    public string HomeAvailabilitySubText => string.Format(AppText.HomeAvailabilitySubFormat, TotalSeats);
    public bool IsHomeAvailabilitySubVisible => IsOpen;
    public double HomeOccupancyRate => IsOpen ? OccupancyRate : 0;
    public Color HomeOccupancyColor => IsOpen ? OccupancyColor : Color.FromArgb(AppConfigProvider.Current.Occupancy.ClosedColor);

    private bool IsWithinStudentAccessHours
    {
        get
        {
            var time = ReferenceTime.TimeOfDay;
            return time >= AppConfigProvider.Current.StudentAccess.Start &&
                   time <= AppConfigProvider.Current.StudentAccess.End;
        }
    }

    private bool IsStudentAccessLocation
    {
        get
        {
            if (SubSpaces.Any(space => AppConfigProvider.Current.StudentAccess.LocationIds
                    .Any(id => string.Equals(space.Id, id, StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(BuildingNumber) &&
                AppConfigProvider.Current.StudentAccess.BuildingIds
                    .Any(id => string.Equals(BuildingNumber.Trim(), id, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return AppConfigProvider.Current.StudentAccess.NameContains
                .Any(token => Name.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }

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

    // --- NEU: Favoriten-Logik ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteIcon))]
    [NotifyPropertyChangedFor(nameof(FavoriteColor))]
    private bool _isFavorite;

    // Diese Properties steuern das Aussehen des Sterns in der UI automatisch!
    public string FavoriteIcon => IsFavorite ? AppText.FavoriteIconFilled : AppText.FavoriteIconOutline;
    public Color FavoriteColor => IsFavorite
        ? Color.FromArgb(AppConfigProvider.Current.UiColors.FavoriteOnColor)
        : Color.FromArgb(AppConfigProvider.Current.UiColors.FavoriteOffColor);
}
