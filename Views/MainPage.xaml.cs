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
        _viewModel.Filters.PropertyChanged += OnFiltersPropertyChanged;
        _viewModel.Settings.PropertyChanged += OnSettingsPropertyChanged;
        _viewModel.Navigation.PropertyChanged += OnNavigationPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_viewModel.SeatList.UiLocations.Count == 0)
        {
            await Task.Delay(AppConfigProvider.Current.UiNumbers.InitialLoadDelayMs);
            await _viewModel.SeatList.LoadSpacesAsync();
        }
        else
        {
            await _viewModel.SeatList.RefreshIfStaleAsync();
        }

        if (_viewModel.Filters.IsBeforeMode && PastTimeFilterPanel != null)
        {
            PastTimeFilterPanel.Opacity = 1;
            PastTimeFilterPanel.TranslationY = 0;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.Settings.IsAboutOpen)
        {
            _viewModel.Settings.IsAboutOpen = false;
            return true;
        }

        if (_viewModel.Filters.IsFilterExpanded)
        {
            _viewModel.Filters.IsFilterExpanded = false;
            return true;
        }

        if (_viewModel.Filters.IsSearchActive)
        {
            _viewModel.Filters.IsSearchActive = false;
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.IsAboutOpen) &&
            _viewModel.Settings.IsAboutOpen &&
            AboutCloseButton != null)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AboutCloseButton.Focus();
            });
        }
    }

    private void OnNavigationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationViewModel.CurrentTab))
        {
            return;
        }

        if (_viewModel.Settings.IsAboutOpen && _viewModel.Navigation.IsMainContentVisible)
        {
            _viewModel.Settings.IsAboutOpen = false;
        }
    }

    private async void OnFiltersPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterViewModel.IsFilterExpanded) &&
            _viewModel.Filters.IsFilterExpanded &&
            FilterSheetScroll != null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await FilterSheetScroll.ScrollToAsync(0, 0, false);
            });
        }

        if (e.PropertyName != nameof(FilterViewModel.IsBeforeMode) || !_viewModel.Filters.IsBeforeMode || PastTimeFilterPanel == null)
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
