using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlatzPilot.Configuration;

namespace PlatzPilot.ViewModels;

public enum FilterChangeKind
{
    PreviewOnly,
    ImmediateApply
}

public sealed class FilterChangedEventArgs : EventArgs
{
    public FilterChangedEventArgs(FilterChangeKind changeKind)
    {
        ChangeKind = changeKind;
    }

    public FilterChangeKind ChangeKind { get; }
}

public partial class FilterViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private bool _isUpdatingDateTimeSelection;

    [ObservableProperty]
    private bool _isFilterExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchInactive))]
    private bool _isSearchActive;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBeforeMode))]
    private bool _useNow = true;

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Now.Date;

    [ObservableProperty]
    private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    private bool _isGroupRoomSelected;

    [ObservableProperty]
    private bool _isSilentStudySelected;

    [ObservableProperty]
    private bool _isNoReservationSelected;

    [ObservableProperty]
    private bool _requireFreeWifi;

    [ObservableProperty]
    private bool _requirePowerOutlets;

    [ObservableProperty]
    private bool _requireWhiteboard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinimumOpenHoursText))]
    private double _minimumOpenHours;

    [ObservableProperty]
    private string _selectedSortOption;

    public FilterViewModel(AppConfig config)
    {
        _config = config;
        SortOptions =
        [
            _config.Sort.Relevance,
            _config.Sort.MostFree,
            _config.Sort.MostTotal,
            _config.Sort.Alphabetical
        ];

        _minimumOpenHours = _config.UiNumbers.MinOpeningHours;
        _selectedSortOption = Preferences.Default.Get(_config.Preferences.SortModeKey, _config.Sort.Relevance);

        if (!SortOptions.Contains(_selectedSortOption))
        {
            _selectedSortOption = _config.Sort.Relevance;
        }
    }

    public event EventHandler<FilterChangedEventArgs>? FiltersChanged;

    public List<string> SortOptions { get; }

    public bool IsBeforeMode => !UseNow;
    public bool IsSearchInactive => !IsSearchActive;
    public DateTime MaxSelectableDate => DateTime.Today;
    public double MinimumOpenHoursMin => _config.UiNumbers.MinOpeningHours;
    public double MinimumOpenHoursMax => _config.UiNumbers.MaxOpeningHours;
    public string MinimumOpenHoursText =>
        string.Format(CultureInfo.CurrentCulture, _config.UiText.MinimumOpenHoursFormat, MinimumOpenHours);

    [RelayCommand]
    private void ToggleSearch()
    {
        if (IsFilterExpanded)
        {
            return;
        }

        IsSearchActive = !IsSearchActive;

        if (!IsSearchActive && !string.IsNullOrWhiteSpace(SearchText))
        {
            SearchText = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleFilter()
    {
        var shouldOpen = !IsFilterExpanded;
        IsFilterExpanded = shouldOpen;

        if (!shouldOpen)
        {
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    [RelayCommand]
    private void CloseFilterSheet() => IsFilterExpanded = false;

    [RelayCommand]
    private async Task SetWhenNowAsync()
    {
        SyncSelectedDateTimeToNow();
        UseNow = true;
        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SetWhenBeforeAsync()
    {
        _isUpdatingDateTimeSelection = true;
        if (SelectedDate.Date > DateTime.Today)
        {
            SelectedDate = MaxSelectableDate;
        }

        if (SelectedTime == TimeSpan.Zero)
        {
            SelectedTime = _config.UiNumbers.DefaultBeforeTime;
        }

        _isUpdatingDateTimeSelection = false;
        UseNow = false;
        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
        await Task.CompletedTask;
    }

    public DateTime GetReferenceDateTime()
    {
        return UseNow ? DateTime.Now : SelectedDate.Date + SelectedTime;
    }

    public string GetApiBeforeParameter()
    {
        return UseNow
            ? _config.SeatFinder.NowToken
            : GetReferenceDateTime().ToString(_config.Internal.ApiDateTimeFormat, CultureInfo.InvariantCulture);
    }

    public bool IsAnyFilterActive()
    {
        if (!UseNow)
        {
            return true;
        }

        if (IsGroupRoomSelected || IsSilentStudySelected || IsNoReservationSelected)
        {
            return true;
        }

        if (RequireFreeWifi || RequirePowerOutlets || RequireWhiteboard)
        {
            return true;
        }

        return MinimumOpenHours > _config.UiNumbers.MinOpeningHours;
    }

    public void ResetToDefaults()
    {
        SyncSelectedDateTimeToNow();
        UseNow = true;

        IsGroupRoomSelected = false;
        IsSilentStudySelected = false;
        IsNoReservationSelected = false;
        RequireFreeWifi = false;
        RequirePowerOutlets = false;
        RequireWhiteboard = false;
        MinimumOpenHours = _config.UiNumbers.MinOpeningHours;
    }

    private void SyncSelectedDateTimeToNow()
    {
        _isUpdatingDateTimeSelection = true;
        var now = DateTime.Now;
        SelectedDate = now.Date;
        SelectedTime = GetCurrentTimeRoundedToMinute();
        _isUpdatingDateTimeSelection = false;
    }

    private static TimeSpan GetCurrentTimeRoundedToMinute()
    {
        var now = DateTime.Now;
        return new TimeSpan(now.Hour, now.Minute, 0);
    }

    partial void OnSearchTextChanged(string value) => RaiseFiltersChanged(FilterChangeKind.ImmediateApply);

    partial void OnUseNowChanged(bool value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    partial void OnSelectedDateChanged(DateTime value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        if (value.Date > DateTime.Today)
        {
            _isUpdatingDateTimeSelection = true;
            SelectedDate = MaxSelectableDate;
            _isUpdatingDateTimeSelection = false;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    partial void OnIsGroupRoomSelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    partial void OnIsSilentStudySelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    partial void OnIsNoReservationSelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    partial void OnRequireFreeWifiChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    partial void OnRequirePowerOutletsChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    partial void OnRequireWhiteboardChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);

    partial void OnMinimumOpenHoursChanged(double value)
    {
        var epsilon = _config.UiNumbers.OpeningHoursSliderSnapEpsilon;
        var rounded = Math.Clamp(
            Math.Round(value),
            _config.UiNumbers.MinOpeningHours,
            _config.UiNumbers.MaxOpeningHours);
        if (Math.Abs(rounded - value) > epsilon)
        {
            MinimumOpenHours = rounded;
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    partial void OnSelectedSortOptionChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Preferences.Default.Set(_config.Preferences.SortModeKey, value);
        RaiseFiltersChanged(FilterChangeKind.ImmediateApply);
    }

    private void RaiseFiltersChanged(FilterChangeKind changeKind)
    {
        FiltersChanged?.Invoke(this, new FilterChangedEventArgs(changeKind));
    }
}
