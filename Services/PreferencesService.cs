using Microsoft.Maui.Storage;

namespace PlatzPilot.Services;

public sealed class PreferencesService : IPreferencesService
{
    private const string SelectedCityIdKey = "SelectedCityId";

    public string SelectedCityId
    {
        get => Preferences.Default.Get(SelectedCityIdKey, "karlsruhe");
        set => Preferences.Default.Set(SelectedCityIdKey, value);
    }

    public T Get<T>(string key, T defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void Set<T>(string key, T value)
    {
        Preferences.Default.Set(key, value);
    }
}
