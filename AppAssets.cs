using PlatzPilot.Configuration;

namespace PlatzPilot;

public static class AppAssets
{
    private static UiAssetsConfig Assets => AppConfigProvider.Current.UiAssets;

    public static string SearchIconLight => Assets.SearchIconLight;
    public static string SearchIconDark => Assets.SearchIconDark;
    public static string FilterIconLight => Assets.FilterIconLight;
    public static string FilterIconDark => Assets.FilterIconDark;
    public static string FavoriteIconOutline => Assets.FavoriteIconOutline;
    public static string FavoriteIconFilled => Assets.FavoriteIconFilled;
    public static string DarkModeIconLight => Assets.DarkModeIconLight;
    public static string DarkModeIconDark => Assets.DarkModeIconDark;
    public static string SettingsLogo => Assets.SettingsLogo;
    public static string HomeIconLight => Assets.HomeIconLight;
    public static string HomeIconDark => Assets.HomeIconDark;
    public static string FavoritesIconLight => Assets.FavoritesIconLight;
    public static string FavoritesIconDark => Assets.FavoritesIconDark;
    public static string SettingsIconLight => Assets.SettingsIconLight;
    public static string SettingsIconDark => Assets.SettingsIconDark;
}
