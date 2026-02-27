using System.Windows.Input;
using System.Globalization; // WICHTIG für die Formatierung der Koordinaten!
using Microsoft.Maui.ApplicationModel;
using PlatzPilot.Models;

namespace PlatzPilot.Views;

[QueryProperty(nameof(LocationData), "LocationData")]
public partial class DetailPage : ContentPage
{
    private UiLocation? _locationData;
    public UiLocation? LocationData
    {
        get => _locationData;
        set { _locationData = value; OnPropertyChanged(); }
    }

    // --- KUGELSICHERER WEB-BEFEHL ---
    public ICommand OpenUrlCommand => new Command<string>(async (url) =>
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        
        try 
        {
            // Wenn die URL nackt ist (z.B. "www.kit.edu"), stürzt der Launcher ab.
            // Wir hängen das Protokoll vorne an, falls es fehlt.
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            // Verhindert einen App-Absturz, falls die URL komplett ungültig ist
            Console.WriteLine($"URL Fehler: {ex.Message}");
        }
    });

    // --- KUGELSICHERER KARTEN-BEFEHL ---
    public ICommand OpenMapCommand => new Command(async () =>
    {
        if (LocationData == null || !LocationData.HasLocation) return;
        
        try 
        {
            // Wir verpacken die Koordinaten in ein offizielles Location-Objekt
            var location = new Location(LocationData.Latitude, LocationData.Longitude);
            var options = new MapLaunchOptions { Name = LocationData.Name };
            
            await Map.Default.OpenAsync(location, options);
        } 
        catch (Exception) 
        {
            // Fallback: Google Maps im Browser (mit offizieller Google Maps Such-URL)
            try 
            {
                var lat = LocationData.Latitude.ToString(CultureInfo.InvariantCulture);
                var lon = LocationData.Longitude.ToString(CultureInfo.InvariantCulture);
                var mapUrl = $"https://www.google.com/maps/search/?api=1&query={lat},{lon}";
                
                await Launcher.Default.OpenAsync(new Uri(mapUrl));
            }
            catch { /* Letztes Sicherheitsnetz */ }
        }
    });

    public ICommand GoBackCommand => new Command(async () =>
    {
        await Shell.Current.GoToAsync("..");
    });

    public DetailPage()
    {
        InitializeComponent();
        BindingContext = this;
    }
}