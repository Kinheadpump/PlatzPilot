using PlatzPilot.Configuration;

namespace PlatzPilot;

public static class AppInfoValues
{
    private static AppInfoConfig Info => AppConfigProvider.Current.AppInfo;

    public static string Name => Info.Name;
    public static string Version => Info.Version;
}
