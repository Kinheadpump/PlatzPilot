using Microsoft.Maui.Storage;

namespace PlatzPilot.Services;

public sealed class PreferencesService : IPreferencesService
{
    public T Get<T>(string key, T defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void Set<T>(string key, T value)
    {
        Preferences.Default.Set(key, value);
    }
}
