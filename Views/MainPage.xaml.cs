using PlatzPilot.ViewModels;

namespace PlatzPilot.Views;

public partial class MainPage : ContentPage
{
    private readonly MainPageViewModel _viewModel;

    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Wird aufgerufen, wenn die Seite im Vordergrund erscheint.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Falls die Liste noch leer ist (erster Start), laden wir die Daten.
        // Wir nutzen einen kleinen Delay, damit das UI Zeit hat zu rendern 
        // und der Refresh-Indikator flüssig angezeigt wird.
        if (_viewModel.UiLocations.Count == 0)
        {
            await Task.Delay(100);
            await _viewModel.LoadSpacesAsync();
        }
    }
}