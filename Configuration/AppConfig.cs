using System;
using System.Collections.Generic;

namespace PlatzPilot.Configuration;

public sealed class AppConfig
{
    public UiNumbersConfig UiNumbers { get; set; } = new();
    public PreferencesConfig Preferences { get; set; } = new();
    public UrlConfig Urls { get; set; } = new();
    public SeatFinderConfig SeatFinder { get; set; } = new();
    public SafeArrivalConfig SafeArrival { get; set; } = new();
    public MensaForecastConfig MensaForecast { get; set; } = new();
    public StudentAccessConfig StudentAccess { get; set; } = new();
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
