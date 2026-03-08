using System.Collections.Generic;
using System.Linq;
using PlatzPilot.Configuration;
using PlatzPilot.Constants;

namespace PlatzPilot.Tests;

internal static class TestConfigFactory
{
    public static CityConfig CreateCity(string id, string displayName, params string[] locations)
    {
        return new CityConfig
        {
            Id = id,
            DisplayName = displayName,
            Locations = locations.Length == 0 ? new List<string> { "L1" } : locations.ToList()
        };
    }

    public static AppConfig Create(params CityConfig[] cities)
    {
        var resolvedCities = (cities == null || cities.Length == 0)
            ? new List<CityConfig> { CreateCity(CityIds.Karlsruhe, "Karlsruhe", "L1") }
            : cities.ToList();

        var config = new AppConfig
        {
            Preferences = new PreferencesConfig
            {
                FavoritesKey = "favorites",
                SortModeKey = "sortMode",
                TabModeKey = "tabMode",
                ThemeKey = "theme",
                LanguageKey = "language",
                ColorBlindModeKey = "colorBlindMode",
                CampusSouthOnlyKey = "campusSouthOnly",
                HapticFeedbackKey = "hapticFeedback",
                HideClosedLocationsKey = "hideClosed",
                OnboardingCompletedKey = "HasCompletedOnboarding"
            },
            Sort = new SortConfig
            {
                Relevance = "relevance",
                MostFree = "mostFree",
                MostTotal = "mostTotal",
                Alphabetical = "alphabetical"
            },
            Tabs = new TabConfig
            {
                Home = "home",
                Favorites = "favorites",
                Settings = "settings"
            },
            SeatFinder = new SeatFinderConfig
            {
                BaseUrl = "https://example.test/{0}/seatfinder",
                NowToken = "now",
                Cities = resolvedCities,
                CallbackPrefix = "PlatzPilot_",
                LocationSeparator = ",",
                QueryStartSeparator = "?",
                QueryPairSeparator = "=",
                QueryParameterSeparator = "&",
                RequestTimeoutSeconds = 1,
                JsonpMinBlocks = 2,
                Query = new SeatFinderQueryConfig
                {
                    CallbackParam = "callback",
                    TimestampParam = "_",
                    Location0Param = "location[0]",
                    Values0Param = "values[0]",
                    After0Param = "after[0]",
                    Before0Param = "before[0]",
                    Limit0Param = "limit[0]",
                    Location1Param = "location[1]",
                    Values1Param = "values[1]",
                    After1Param = "after[1]",
                    Before1Param = "before[1]",
                    Limit1Param = "limit[1]",
                    Values0Value = "seatestimate,manualcount",
                    Values1Value = "location"
                }
            },
            Internal = new InternalConfig
            {
                ApiDateTimeFormat = "yyyy-MM-ddTHH:mm:ss",
                JsonpParseErrorText = "Parse error",
                HttpRequestErrorFormat = "Request error: {0}"
            },
            Theme = new ThemeConfig
            {
                System = "system",
                Light = "light",
                Dark = "dark"
            }
        };

        config.UiNumbers.OfflineBannerDurationMs = 1;
        config.UiNumbers.SkeletonItemCount = 1;

        return config;
    }
}
