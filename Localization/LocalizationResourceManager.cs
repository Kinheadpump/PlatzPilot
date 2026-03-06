using System.ComponentModel;
using System.Globalization;
using System.Threading;
using PlatzPilot.Resources.Strings;

namespace PlatzPilot.Localization;

public sealed class LocalizationResourceManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationResourceManager> InstanceHolder =
        new(() => new LocalizationResourceManager());

    private readonly object _lock = new();
    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public static LocalizationResourceManager Instance => InstanceHolder.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string resourceKey]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return string.Empty;
            }

            var value = AppResources.ResourceManager.GetString(resourceKey, _culture);
            if (string.IsNullOrEmpty(value) && _culture != CultureInfo.InvariantCulture)
            {
                value = AppResources.ResourceManager.GetString(resourceKey, CultureInfo.InvariantCulture);
            }

            return value ?? resourceKey;
        }
    }

    public void SetCulture(CultureInfo culture)
    {
        if (culture == null)
        {
            return;
        }

        lock (_lock)
        {
            _culture = culture;
            AppResources.Culture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
