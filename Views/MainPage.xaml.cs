using System.ComponentModel;
using PlatzPilot.Configuration;
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
            await Task.Delay(AppConfigProvider.Current.UiNumbers.InitialLoadDelayMs);
            await _viewModel.LoadSpacesAsync();
        }

        if (_viewModel.IsBeforeMode && PastTimeFilterPanel != null)
        {
            PastTimeFilterPanel.Opacity = 1;
            PastTimeFilterPanel.TranslationY = 0;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsAboutOpen)
        {
            _viewModel.IsAboutOpen = false;
            return true;
        }

        if (_viewModel.IsFilterExpanded)
        {
            _viewModel.IsFilterExpanded = false;
            return true;
        }

        if (_viewModel.IsSearchActive)
        {
            _viewModel.IsSearchActive = false;
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageViewModel.IsAboutOpen) &&
            _viewModel.IsAboutOpen &&
            AboutCloseButton != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AboutCloseButton.Focus();
            });
        }

        if (e.PropertyName == nameof(MainPageViewModel.IsFilterExpanded) &&
            _viewModel.IsFilterExpanded &&
            FilterSheetScroll != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await FilterSheetScroll.ScrollToAsync(0, 0, false);
            });
        }

        if (e.PropertyName != nameof(MainPageViewModel.IsBeforeMode) || !_viewModel.IsBeforeMode || PastTimeFilterPanel == null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            PastTimeFilterPanel.Opacity = 0;
            PastTimeFilterPanel.TranslationY = AppConfigProvider.Current.UiNumbers.FilterSheetTranslationOffset;

            var duration = (uint)AppConfigProvider.Current.UiNumbers.FilterSheetAnimationDurationMs;
            await Task.WhenAll(
                PastTimeFilterPanel.FadeToAsync(1, duration, Easing.CubicOut),
                PastTimeFilterPanel.TranslateToAsync(0, 0, duration, Easing.CubicOut));
        });
    }

    private void OnOpeningHoursSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        var config = AppConfigProvider.Current.UiNumbers;
        var snappedValue = Math.Round(e.NewValue, MidpointRounding.AwayFromZero);
        snappedValue = Math.Clamp(snappedValue, config.MinOpeningHours, config.MaxOpeningHours);
        if (Math.Abs(slider.Value - snappedValue) < config.OpeningHoursSliderSnapEpsilon)
        {
            return;
        }

        slider.Value = snappedValue;
    }
}
