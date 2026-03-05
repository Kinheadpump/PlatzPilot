namespace PlatzPilot.Configuration;

public sealed class PreferencesConfig
{
    public string FavoritesKey { get; set; } = string.Empty;
    public string SortModeKey { get; set; } = string.Empty;
    public string TabModeKey { get; set; } = string.Empty;
    public string ThemeKey { get; set; } = string.Empty;
    public string ColorBlindModeKey { get; set; } = string.Empty;
    public string CampusSouthOnlyKey { get; set; } = string.Empty;
    public string HapticFeedbackKey { get; set; } = string.Empty;
    public string HideClosedLocationsKey { get; set; } = string.Empty;
    public string OnboardingCompletedKey { get; set; } = string.Empty;
    public string EmptyListJson { get; set; } = "[]";
}
