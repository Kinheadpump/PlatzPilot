using System.ComponentModel;
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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.UiLocations.Count == 0)
        {
            await Task.Delay(100);
            await _viewModel.LoadSpacesAsync();
        }

        if (_viewModel.IsBeforeMode && PastTimeFilterPanel != null)
        {
            PastTimeFilterPanel.Opacity = 1;
            PastTimeFilterPanel.TranslationY = 0;
        }
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainPageViewModel.IsBeforeMode) || !_viewModel.IsBeforeMode || PastTimeFilterPanel == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            PastTimeFilterPanel.Opacity = 0;
            PastTimeFilterPanel.TranslationY = -8;

            await Task.WhenAll(
                PastTimeFilterPanel.FadeToAsync(1, 160, Easing.CubicOut),
                PastTimeFilterPanel.TranslateToAsync(0, 0, 160, Easing.CubicOut));
        });
    }

    private void OnOpeningHoursSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        var snappedValue = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
        snappedValue = Math.Clamp(snappedValue, 0, 12);
        if (Math.Abs(slider.Value - snappedValue) < 0.001)
        {
            return;
        }

        slider.Value = snappedValue;
    }
}
