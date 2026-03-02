using PlatzPilot.Configuration;

namespace PlatzPilot;

public static class AppTabs
{
    private static TabConfig Tabs => AppConfigProvider.Current.Tabs;

    public static string Home => Tabs.Home;
    public static string Favorites => Tabs.Favorites;
    public static string Settings => Tabs.Settings;
}
