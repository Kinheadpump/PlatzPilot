namespace PlatzPilot.Services;

public interface IPreferencesService
{
    string SelectedCityId { get; set; }
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
}
