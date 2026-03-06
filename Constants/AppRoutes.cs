using PlatzPilot.Configuration;

namespace PlatzPilot.Constants;

public static class AppRoutes
{
    private static InternalConfig Internal => AppConfigProvider.Current.Internal;

    public static string MainPage => Internal.MainPageRoute;
    public static string DetailPage => Internal.DetailPageRoute;
    public static string BackNavigation => Internal.BackNavigationRoute;
}
