using System.Collections.Generic;

namespace PlatzPilot.Configuration;

public sealed class AppConfig
{
    public UiTextConfig UiText { get; set; } = new();
    public UiNumbersConfig UiNumbers { get; set; } = new();
    public PreferencesConfig Preferences { get; set; } = new();
    public UrlConfig Urls { get; set; } = new();
    public SeatFinderConfig SeatFinder { get; set; } = new();
    public SafeArrivalConfig SafeArrival { get; set; } = new();
    public StudentAccessConfig StudentAccess { get; set; } = new();
    public OpeningHoursTextConfig OpeningHoursText { get; set; } = new();
    public OpeningHoursSettings OpeningHours { get; set; } = new();
    public RoomTypeConfig RoomTypes { get; set; } = new();
    public SortConfig Sort { get; set; } = new();
    public TabConfig Tabs { get; set; } = new();
    public ThemeConfig Theme { get; set; } = new();
    public FontsConfig Fonts { get; set; } = new();
    public AppInfoConfig AppInfo { get; set; } = new();
    public OccupancyConfig Occupancy { get; set; } = new();
    public ColorBlindnessConfig ColorBlindness { get; set; } = new();
    public CampusSouthConfig CampusSouth { get; set; } = new();
    public UiColorsConfig UiColors { get; set; } = new();
    public UiAssetsConfig UiAssets { get; set; } = new();
    public ChartConfig Charts { get; set; } = new();
    public InternalConfig Internal { get; set; } = new();
    public Dictionary<string, string> BuildingNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class UiTextConfig
{
    public string HomeTitle { get; set; } = string.Empty;
    public string HomeTabLabel { get; set; } = string.Empty;
    public string SearchPlaceholder { get; set; } = string.Empty;
    public string EmptyListText { get; set; } = string.Empty;
    public string FavoritesEmptyText { get; set; } = string.Empty;
    public string OfflineBannerText { get; set; } = string.Empty;
    public string ClosedLabel { get; set; } = string.Empty;
    public string ClosedStudentsLabel { get; set; } = string.Empty;
    public string ManualCountLabel { get; set; } = string.Empty;
    public string AvailabilityFormat { get; set; } = string.Empty;
    public string NoCurrentInfoText { get; set; } = string.Empty;
    public string RecommendationNoneText { get; set; } = string.Empty;
    public string RecommendationFormat { get; set; } = string.Empty;
    public string PeakNoneText { get; set; } = string.Empty;
    public string PeakFormat { get; set; } = string.Empty;
    public string QualityPrefix { get; set; } = string.Empty;
    public string QualityFormat { get; set; } = string.Empty;
    public string QualityLow { get; set; } = string.Empty;
    public string QualityMedium { get; set; } = string.Empty;
    public string QualityHigh { get; set; } = string.Empty;
    public string PeakTrendFormat { get; set; } = string.Empty;
    public string PeakTrendEarlier { get; set; } = string.Empty;
    public string PeakTrendLater { get; set; } = string.Empty;
    public string PeakTrendFlat { get; set; } = string.Empty;
    public string FavoritesLabel { get; set; } = string.Empty;
    public string SettingsLabel { get; set; } = string.Empty;
    public string FilterTitle { get; set; } = string.Empty;
    public string FilterReset { get; set; } = string.Empty;
    public string WhenTitle { get; set; } = string.Empty;
    public string WhenNow { get; set; } = string.Empty;
    public string WhenBefore { get; set; } = string.Empty;
    public string RoomTypeTitle { get; set; } = string.Empty;
    public string RoomTypeGroup { get; set; } = string.Empty;
    public string RoomTypeSilent { get; set; } = string.Empty;
    public string RoomTypeNoReservation { get; set; } = string.Empty;
    public string EquipmentTitle { get; set; } = string.Empty;
    public string EquipmentWifi { get; set; } = string.Empty;
    public string EquipmentPower { get; set; } = string.Empty;
    public string EquipmentWhiteboard { get; set; } = string.Empty;
    public string OpeningHoursTitle { get; set; } = string.Empty;
    public string SortTitle { get; set; } = string.Empty;
    public string SortByLabel { get; set; } = string.Empty;
    public string ShowResultsFormat { get; set; } = string.Empty;
    public string MinimumOpenHoursFormat { get; set; } = string.Empty;
    public string OpenMapLabel { get; set; } = string.Empty;
    public string OpenWebsiteLabel { get; set; } = string.Empty;
    public string BuildingLabel { get; set; } = string.Empty;
    public string BuildingFormat { get; set; } = string.Empty;
    public string SingleLocationSubtitle { get; set; } = string.Empty;
    public string GroupedLocationSubtitleFormat { get; set; } = string.Empty;
    public string OpeningHoursLabel { get; set; } = string.Empty;
    public string LastCountLabel { get; set; } = string.Empty;
    public string OccupancyInfoTitle { get; set; } = string.Empty;
    public string OccupancyLabel { get; set; } = string.Empty;
    public string RoomsInBuildingLabel { get; set; } = string.Empty;
    public string AvailabilitySuffix { get; set; } = string.Empty;
    public string HomeAvailabilitySubFormat { get; set; } = string.Empty;
    public string LastUpdatedFormat { get; set; } = string.Empty;
    public string LastUpdatedUnknownText { get; set; } = string.Empty;
    public string SettingsTitle { get; set; } = string.Empty;
    public string SettingsDesignLabel { get; set; } = string.Empty;
    public string SettingsDisplayTitle { get; set; } = string.Empty;
    public string SettingsFeedbackTitle { get; set; } = string.Empty;
    public string SettingsHapticLabel { get; set; } = string.Empty;
    public string SettingsVersionFormat { get; set; } = string.Empty;
    public string SettingsDisclaimer { get; set; } = string.Empty;
    public string GithubLabel { get; set; } = string.Empty;
    public string ImpressumLabel { get; set; } = string.Empty;
    public string PrivacyLabel { get; set; } = string.Empty;
    public string LicensesLabel { get; set; } = string.Empty;
    public string LicensesTitle { get; set; } = string.Empty;
    public string LicensesText { get; set; } = string.Empty;
    public string ColorBlindModeLabel { get; set; } = string.Empty;
    public string CampusSouthOnlyLabel { get; set; } = string.Empty;
    public string BackGlyph { get; set; } = string.Empty;
    public string OkButtonLabel { get; set; } = string.Empty;
    public string FavoriteIconFilled { get; set; } = string.Empty;
    public string FavoriteIconOutline { get; set; } = string.Empty;
    public string LevelFormat { get; set; } = string.Empty;
    public string RoomFormat { get; set; } = string.Empty;
    public string BuildingUnknownText { get; set; } = string.Empty;
    public string ChartLabel6Hours { get; set; } = string.Empty;
    public string ChartLabel12Hours { get; set; } = string.Empty;
    public string ChartLabel18Hours { get; set; } = string.Empty;
    public string ChartLabel24Hours { get; set; } = string.Empty;
}

public sealed class UiNumbersConfig
{
    public int MinOpeningHours { get; set; } = 0;
    public int MaxOpeningHours { get; set; } = 12;
    public int UnknownYearThreshold { get; set; } = 2000;
    public int StudentClosedLabelTopMargin { get; set; } = 4;
    public TimeSpan DefaultBeforeTime { get; set; } = new(12, 0, 0);
    public int InitialLoadDelayMs { get; set; } = 100;
    public int FilterSheetAnimationDurationMs { get; set; } = 160;
    public double FilterSheetTranslationOffset { get; set; } = -8;
    public double OpeningHoursSliderSnapEpsilon { get; set; } = 0.001;
}

public sealed class PreferencesConfig
{
    public string FavoritesKey { get; set; } = string.Empty;
    public string SortModeKey { get; set; } = string.Empty;
    public string TabModeKey { get; set; } = string.Empty;
    public string ThemeKey { get; set; } = string.Empty;
    public string ColorBlindModeKey { get; set; } = string.Empty;
    public string CampusSouthOnlyKey { get; set; } = string.Empty;
    public string HapticFeedbackKey { get; set; } = string.Empty;
    public string EmptyListJson { get; set; } = "[]";
}

public sealed class UrlConfig
{
    public string Github { get; set; } = string.Empty;
    public string Privacy { get; set; } = string.Empty;
    public string Impressum { get; set; } = string.Empty;
}

public sealed class SeatFinderConfig
{
    public string BaseUrl { get; set; } = string.Empty;
    public string NowToken { get; set; } = string.Empty;
    public List<string> Locations { get; set; } = [];
    public int WeeklyHistoryPoints { get; set; } = 2016;
    public int LiveSnapshotPoints { get; set; } = 1;
    public int LiveRefreshIntervalMinutes { get; set; } = 5;
    public int MetadataLimit { get; set; } = 1;
    public string CallbackPrefix { get; set; } = string.Empty;
    public string TimestampFormat { get; set; } = string.Empty;
    public string LocationSeparator { get; set; } = string.Empty;
    public string CoordinateSeparator { get; set; } = string.Empty;
    public string QueryStartSeparator { get; set; } = string.Empty;
    public string QueryPairSeparator { get; set; } = string.Empty;
    public string QueryParameterSeparator { get; set; } = string.Empty;
    public SeatFinderQueryConfig Query { get; set; } = new();
    public string SpaceFeaturesFileName { get; set; } = string.Empty;
}

public sealed class SeatFinderQueryConfig
{
    public string CallbackParam { get; set; } = string.Empty;
    public string TimestampParam { get; set; } = string.Empty;
    public string Location0Param { get; set; } = string.Empty;
    public string Values0Param { get; set; } = string.Empty;
    public string After0Param { get; set; } = string.Empty;
    public string Before0Param { get; set; } = string.Empty;
    public string Limit0Param { get; set; } = string.Empty;
    public string Location1Param { get; set; } = string.Empty;
    public string Values1Param { get; set; } = string.Empty;
    public string After1Param { get; set; } = string.Empty;
    public string Before1Param { get; set; } = string.Empty;
    public string Limit1Param { get; set; } = string.Empty;
    public string Values0Value { get; set; } = string.Empty;
    public string Values1Value { get; set; } = string.Empty;
}

public sealed class SafeArrivalConfig
{
    public int BinMinutes { get; set; } = 5;
    public int SmoothingWindow { get; set; } = 5;
    public double AlphaPrior { get; set; } = 1.0;
    public double BetaPrior { get; set; } = 1.0;
    public int RequiredFreeSeats { get; set; } = 1;
    public double GoalProbability { get; set; } = 0.85;
    public double TauDays { get; set; } = 2.5;
    public double BufferRatio { get; set; } = 0.08;
    public double MinWeightedSamplesForCandidate { get; set; } = 0.2;
    public double EarlyPeakQuantile { get; set; } = 0.45;
    public double EarlyPeakBias { get; set; } = 0.7;
    public double DailyPeakThresholdRatio { get; set; } = 0.85;
    public int LeadTimeBeforePeakMinutes { get; set; } = 15;
    public int MinCoveredDaysForConfidence { get; set; } = 2;
    public double MinWeightedSamplesForConfidence { get; set; } = 1.0;
    public double FallbackProbabilityMargin { get; set; } = 0.18;
    public double MinFallbackProbability { get; set; } = 0.65;
    public int HistoryWindowDays { get; set; } = 7;
    public int DayCoverageMaxOffsetDays { get; set; } = 30;
    public double EarlyPeakQuantileMin { get; set; } = 0.05;
    public double EarlyPeakQuantileMax { get; set; } = 0.95;
    public double TrendFlatThresholdMinutes { get; set; } = 2.0;
    public double DailyPeakThresholdMin { get; set; } = 0.6;
    public double DailyPeakThresholdMax { get; set; } = 0.98;
    public double HighProbabilityThreshold { get; set; } = 0.90;
    public double MediumProbabilityThreshold { get; set; } = 0.80;
}

public sealed class StudentAccessConfig
{
    public TimeSpan Start { get; set; } = new(7, 0, 0);
    public TimeSpan End { get; set; } = new(22, 0, 0);
    public List<string> LocationIds { get; set; } = [];
    public List<string> BuildingIds { get; set; } = [];
    public List<string> NameContains { get; set; } = [];
}

public sealed class OpeningHoursTextConfig
{
    public string ClosedSpecialText { get; set; } = string.Empty;
    public string UnknownText { get; set; } = string.Empty;
    public string AlwaysOpenText { get; set; } = string.Empty;
    public string ClosedText { get; set; } = string.Empty;
    public string HoursSuffix { get; set; } = string.Empty;
    public string TimeRangeFormat { get; set; } = string.Empty;
    public string TimeRangeSeparator { get; set; } = string.Empty;
    public string ClosedOpensTodayFormat { get; set; } = string.Empty;
    public string ClosedOpensTomorrowFormat { get; set; } = string.Empty;
    public string ClosedOpensOnDateFormat { get; set; } = string.Empty;
    public string NoRoomsText { get; set; } = string.Empty;
    public string UnknownObjectText { get; set; } = string.Empty;
}

public sealed class OpeningHoursSettings
{
    public int MaxNextOpeningWeeks { get; set; } = 3;
    public int DaysPerWeek { get; set; } = 7;
    public int FullDayHours { get; set; } = 24;
    public int AlwaysOpenHourThreshold { get; set; } = 23;
}

public sealed class RoomTypeConfig
{
    public string Group { get; set; } = string.Empty;
    public string SilentStudy { get; set; } = string.Empty;
    public string SilentStudyLegacy { get; set; } = string.Empty;
    public string NoReservation { get; set; } = string.Empty;
    public string NoReservationLegacy { get; set; } = string.Empty;
}

public sealed class SortConfig
{
    public string Relevance { get; set; } = string.Empty;
    public string MostFree { get; set; } = string.Empty;
    public string MostTotal { get; set; } = string.Empty;
    public string Alphabetical { get; set; } = string.Empty;
}

public sealed class TabConfig
{
    public string Home { get; set; } = string.Empty;
    public string Favorites { get; set; } = string.Empty;
    public string Settings { get; set; } = string.Empty;
}

public sealed class ThemeConfig
{
    public string Light { get; set; } = string.Empty;
    public string Dark { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
}

public sealed class FontsConfig
{
    public List<FontEntry> Entries { get; set; } = [];
}

public sealed class FontEntry
{
    public string FileName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
}

public sealed class AppInfoConfig
{
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class OccupancyConfig
{
    public double LowThreshold { get; set; } = 0.4;
    public double MediumThreshold { get; set; } = 0.7;
    public double HighThreshold { get; set; } = 0.9;
    public string LowColor { get; set; } = "#2ecc71";
    public string MediumColor { get; set; } = "#f1c40f";
    public string HighColor { get; set; } = "#e67e22";
    public string FullColor { get; set; } = "#e74c3c";
    public string ClosedColor { get; set; } = "#b0b0b0";
}

public sealed class ColorBlindnessConfig
{
    public string LowColor { get; set; } = "#648FFF";
    public string MediumColor { get; set; } = "#FFB000";
    public string HighColor { get; set; } = "#DC267F";
    public string FullColor { get; set; } = "#DC267F";
}

public sealed class CampusSouthConfig
{
    public List<string> ExcludedNameContains { get; set; } = [];
}

public sealed class UiColorsConfig
{
    public string FavoriteOnColor { get; set; } = "#f1c40f";
    public string FavoriteOffColor { get; set; } = "#808080";
}

public sealed class UiAssetsConfig
{
    public string SearchIconLight { get; set; } = string.Empty;
    public string SearchIconDark { get; set; } = string.Empty;
    public string FilterIconLight { get; set; } = string.Empty;
    public string FilterIconDark { get; set; } = string.Empty;
    public string FavoriteIconOutline { get; set; } = string.Empty;
    public string FavoriteIconFilled { get; set; } = string.Empty;
    public string DarkModeIconLight { get; set; } = string.Empty;
    public string DarkModeIconDark { get; set; } = string.Empty;
    public string SettingsLogo { get; set; } = string.Empty;
    public string HomeIconLight { get; set; } = string.Empty;
    public string HomeIconDark { get; set; } = string.Empty;
    public string FavoritesIconLight { get; set; } = string.Empty;
    public string FavoritesIconDark { get; set; } = string.Empty;
    public string SettingsIconLight { get; set; } = string.Empty;
    public string SettingsIconDark { get; set; } = string.Empty;
    public string ColorBlindOnIconLight { get; set; } = string.Empty;
    public string ColorBlindOnIconDark { get; set; } = string.Empty;
    public string ColorBlindOffIconLight { get; set; } = string.Empty;
    public string ColorBlindOffIconDark { get; set; } = string.Empty;
    public string CampusSouthOnIconLight { get; set; } = string.Empty;
    public string CampusSouthOnIconDark { get; set; } = string.Empty;
    public string CampusSouthOffIconLight { get; set; } = string.Empty;
    public string CampusSouthOffIconDark { get; set; } = string.Empty;
}

public sealed class ChartConfig
{
    public int HistoryHours { get; set; } = 24;
    public int BinMinutes { get; set; } = 5;
    public double Height { get; set; } = 120;
    public float LineSize { get; set; } = 2f;
    public byte LineAreaAlpha { get; set; } = 38;
    public float LabelTextSize { get; set; } = 0f;
    public float ValueLabelTextSize { get; set; } = 0f;
    public float SerieLabelTextSize { get; set; } = 0f;
    public bool ShowYAxisLines { get; set; }
    public bool ShowYAxisText { get; set; }
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public string LineColorLight { get; set; } = "#1f6feb";
    public string LineColorDark { get; set; } = "#9ec1ff";
    public string BackgroundColorLight { get; set; } = "#ffffff";
    public string BackgroundColorDark { get; set; } = "#1e1e1e";
}

public sealed class InternalConfig
{
    public string BuildingAggregateIdPrefix { get; set; } = string.Empty;
    public string BuildingAggregateIdSeparator { get; set; } = string.Empty;
    public string BuildingAggregateName { get; set; } = string.Empty;
    public string ApiDateTimeFormat { get; set; } = string.Empty;
    public string DatePickerFormat { get; set; } = string.Empty;
    public string TimePickerFormat { get; set; } = string.Empty;
    public string MainPageRoute { get; set; } = string.Empty;
    public string DetailPageRoute { get; set; } = string.Empty;
    public string BackNavigationRoute { get; set; } = string.Empty;
    public string LocationDataKey { get; set; } = string.Empty;
    public string SpaceFeaturesLoadFailedFormat { get; set; } = string.Empty;
    public string ConfigLoadFailedFormat { get; set; } = string.Empty;
    public string JsonpParseErrorText { get; set; } = string.Empty;
    public string HttpRequestErrorFormat { get; set; } = string.Empty;
    public string UrlOpenFailedFormat { get; set; } = string.Empty;
    public string UrlSchemeHttp { get; set; } = string.Empty;
    public string UrlSchemeHttps { get; set; } = string.Empty;
    public string UrlSchemeDefaultPrefix { get; set; } = string.Empty;
    public string MapSearchUrlFormat { get; set; } = string.Empty;
    public string LevelNumberRegex { get; set; } = string.Empty;
}
