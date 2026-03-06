using System.Globalization;
using Microsoft.Maui.Storage;
using PlatzPilot.Configuration;
using PlatzPilot.Localization;

namespace PlatzPilot.Services;

public static class LocalizationService
{
    private const string GermanCulture = "de";
    private const string EnglishCulture = "en";

    public static void ApplySavedLanguage(AppConfig config)
    {
        var saved = Preferences.Default.Get(config.Preferences.LanguageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(saved))
        {
            return;
        }

        ApplyLanguage(saved, config, persist: false);
    }

    public static void ApplyLanguage(string cultureCode, AppConfig config, bool persist = true)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
        {
            return;
        }

        var normalized = NormalizeCulture(cultureCode);
        var culture = new CultureInfo(normalized);
        LocalizationResourceManager.Instance.SetCulture(culture);

        if (persist)
        {
            Preferences.Default.Set(config.Preferences.LanguageKey, normalized);
        }
    }

    private static string NormalizeCulture(string cultureCode)
    {
        if (cultureCode.StartsWith(GermanCulture, StringComparison.OrdinalIgnoreCase))
        {
            return GermanCulture;
        }

        if (cultureCode.StartsWith(EnglishCulture, StringComparison.OrdinalIgnoreCase))
        {
            return EnglishCulture;
        }

        return cultureCode;
    }
}
