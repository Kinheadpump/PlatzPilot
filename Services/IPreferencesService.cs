namespace PlatzPilot.Services;

public interface IPreferencesService
{
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
}
