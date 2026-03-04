using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PlatzPilot.Configuration;

namespace PlatzPilot.Models;

// WICHTIG: Erbt jetzt von ObservableObject und ist partial!
public partial class UiLocation : ObservableObject
{

    public string Name { get; set; } = string.Empty;
    public string TileName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int FreeSeats { get; set; }
    public int OccupiedSeats { get; set; }
    public bool IsManualCount { get; set; }
    public List<StudySpace> SubSpaces { get; set; } = new();
    public string? MainUrl => SubSpaces.FirstOrDefault()?.Url;
    public string? BuildingNumber { get; set; }
    public string? BuildingDisplayOverride { get; set; }
    public string BuildingDisplayText => !string.IsNullOrWhiteSpace(BuildingDisplayOverride)
        ? BuildingDisplayOverride
        : string.IsNullOrWhiteSpace(BuildingNumber)
            ? AppText.BuildingUnknownText
            : BuildingNumber;
    public DateTime ReferenceTime { get; set; } = DateTime.Now;
    public string BestArrivalText { get; set; } = AppText.RecommendationNoneText;
    public bool HasArrivalInsights { get; set; }
    public string PeakAverageText { get; set; } = AppText.PeakNoneText;
    public string SafetyLevelText { get; set; } = string.Format(AppText.QualityFormat, AppText.QualityLow);
    public string PeakTrendText { get; set; } = string.Format(AppText.PeakTrendFormat, AppText.PeakTrendFlat);
    public IReadOnlyList<float> OccupancySeries { get; set; } = Array.Empty<float>();
    public bool IsMensaVirtual { get; set; }
    public double MensaOccupancyRate { get; set; }
    public TimeSpan? MensaOpeningStart { get; set; }
    public TimeSpan? MensaOpeningEnd { get; set; }
    public bool IsSkeleton { get; set; }

    public DateTime? LastUpdated => SubSpaces.Count == 0 ? null : SubSpaces.Max(s => s.LastUpdated);
    
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
    public bool IsDataStale => IsOpen &&
                               LastUpdated.HasValue &&
                               DateTime.Now - LastUpdated.Value > TimeSpan.FromMinutes(30);
    public string StatusText => !IsOpen
        ? ClosedStatusText
        : IsDataStale
            ? AppText.DataStaleText
            : string.Empty;
    public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);
    public Color StatusTextColor => !IsOpen
        ? Color.FromArgb("#e74c3c")
        : Color.FromArgb("#f39c12");
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

            if (IsMensaVirtual && MensaOpeningStart.HasValue && MensaOpeningEnd.HasValue)
            {
                var day = ReferenceTime.DayOfWeek;
                if (day < DayOfWeek.Monday || day > DayOfWeek.Friday)
                {
                    return openingText.ClosedText;
                }

                var start = ReferenceTime.Date.Add(MensaOpeningStart.Value);
                var end = ReferenceTime.Date.Add(MensaOpeningEnd.Value);
                var timeRange = string.Format(
                    CultureInfo.CurrentCulture,
                    openingText.TimeRangeFormat,
                    start,
                    end);
                return timeRange + openingText.HoursSuffix;
            }
            
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
    public string AvailabilityText
    {
        get
        {
            if (IsMensaVirtual && TotalSeats > 0)
            {
                var rate = Math.Clamp(OccupiedSeats / (double)TotalSeats, 0, 1);
                return string.Format(CultureInfo.CurrentCulture, "~ {0:P0} belegt", rate);
            }

            var freeSeats = TotalSeats > 0
                ? Math.Max(0, TotalSeats - OccupiedSeats)
                : FreeSeats;
            return string.Format(AppText.AvailabilityFormat, freeSeats, TotalSeats);
        }
    }

    public string HomeAvailabilityText => IsOpen ? AvailabilityText : AppText.NoCurrentInfoText;
    public string HomeAvailabilitySubText => string.Format(AppText.HomeAvailabilitySubFormat, TotalSeats);
    public bool IsHomeAvailabilitySubVisible => IsOpen;
    public double HomeOccupancyRate
    {
        get
        {
            var rate = IsMensaVirtual ? MensaOccupancyRate : OccupancyRate;
            return IsOpen ? rate : 0;
        }
    }
    public Color HomeOccupancyColor => IsOpen ? OccupancyColor : Color.FromArgb("#00000000");

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
            var config = AppConfigProvider.Current;
            var thresholds = config.Occupancy;
            var isColorBlindMode = Preferences.Default.Get(config.Preferences.ColorBlindModeKey, false);
            var palette = isColorBlindMode ? config.ColorBlindness : null;

            var lowColor = isColorBlindMode ? palette!.LowColor : thresholds.LowColor;
            var mediumColor = isColorBlindMode ? palette!.MediumColor : thresholds.MediumColor;
            var highColor = isColorBlindMode ? palette!.HighColor : thresholds.HighColor;
            var fullColor = isColorBlindMode ? palette!.FullColor : thresholds.FullColor;

            if (OccupancyRate < thresholds.LowThreshold) return Color.FromArgb(lowColor);
            if (OccupancyRate < thresholds.MediumThreshold) return Color.FromArgb(mediumColor);
            if (OccupancyRate < thresholds.HighThreshold) return Color.FromArgb(highColor);
            return Color.FromArgb(fullColor);
        }
    }

    // --- NEU: Favoriten-Logik ---
    private bool _isFavorite;

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (SetProperty(ref _isFavorite, value))
            {
                OnPropertyChanged(nameof(FavoriteIcon));
                OnPropertyChanged(nameof(FavoriteColor));
            }
        }
    }

    // Diese Properties steuern das Aussehen des Sterns in der UI automatisch!
    public string FavoriteIcon => IsFavorite ? AppText.FavoriteIconFilled : AppText.FavoriteIconOutline;
    public Color FavoriteColor => IsFavorite
        ? Color.FromArgb(AppConfigProvider.Current.UiColors.FavoriteOnColor)
        : Color.FromArgb(AppConfigProvider.Current.UiColors.FavoriteOffColor);
}
