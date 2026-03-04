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

    private bool _isFilterExpanded;
    private bool _isSearchActive;
    private string _searchText = string.Empty;
    private bool _useNow = true;
    private DateTime _selectedDate = DateTime.Now.Date;
    private TimeSpan _selectedTime = DateTime.Now.TimeOfDay;
    private bool _isGroupRoomSelected;
    private bool _isSilentStudySelected;
    private bool _isNoReservationSelected;
    private bool _requireFreeWifi;
    private bool _requirePowerOutlets;
    private bool _requireWhiteboard;
    private double _minimumOpenHours;
    private string _selectedSortOption = string.Empty;
    private bool _isFilterActive;

    public bool IsFilterExpanded
    {
        get => _isFilterExpanded;
        set => SetProperty(ref _isFilterExpanded, value);
    }

    public bool IsSearchActive
    {
        get => _isSearchActive;
        set
        {
            if (SetProperty(ref _isSearchActive, value))
            {
                OnPropertyChanged(nameof(IsSearchInactive));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnSearchTextChanged(value);
            }
        }
    }

    public bool UseNow
    {
        get => _useNow;
        set
        {
            if (SetProperty(ref _useNow, value))
            {
                OnPropertyChanged(nameof(IsBeforeMode));
                OnUseNowChanged(value);
            }
        }
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                OnSelectedDateChanged(value);
            }
        }
    }

    public TimeSpan SelectedTime
    {
        get => _selectedTime;
        set
        {
            if (SetProperty(ref _selectedTime, value))
            {
                OnSelectedTimeChanged(value);
            }
        }
    }

    public bool IsGroupRoomSelected
    {
        get => _isGroupRoomSelected;
        set
        {
            if (SetProperty(ref _isGroupRoomSelected, value))
            {
                OnIsGroupRoomSelectedChanged(value);
            }
        }
    }

    public bool IsSilentStudySelected
    {
        get => _isSilentStudySelected;
        set
        {
            if (SetProperty(ref _isSilentStudySelected, value))
            {
                OnIsSilentStudySelectedChanged(value);
            }
        }
    }

    public bool IsNoReservationSelected
    {
        get => _isNoReservationSelected;
        set
        {
            if (SetProperty(ref _isNoReservationSelected, value))
            {
                OnIsNoReservationSelectedChanged(value);
            }
        }
    }

    public bool RequireFreeWifi
    {
        get => _requireFreeWifi;
        set
        {
            if (SetProperty(ref _requireFreeWifi, value))
            {
                OnRequireFreeWifiChanged(value);
            }
        }
    }

    public bool RequirePowerOutlets
    {
        get => _requirePowerOutlets;
        set
        {
            if (SetProperty(ref _requirePowerOutlets, value))
            {
                OnRequirePowerOutletsChanged(value);
            }
        }
    }

    public bool RequireWhiteboard
    {
        get => _requireWhiteboard;
        set
        {
            if (SetProperty(ref _requireWhiteboard, value))
            {
                OnRequireWhiteboardChanged(value);
            }
        }
    }

    public double MinimumOpenHours
    {
        get => _minimumOpenHours;
        set
        {
            if (SetProperty(ref _minimumOpenHours, value))
            {
                OnPropertyChanged(nameof(MinimumOpenHoursText));
                OnMinimumOpenHoursChanged(value);
            }
        }
    }

    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
            {
                OnSelectedSortOptionChanged(value);
            }
        }
    }

    public bool IsFilterActive
    {
        get => _isFilterActive;
        set => SetProperty(ref _isFilterActive, value);
    }

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

    public void MarkFiltersApplied()
    {
        IsFilterActive = IsAnyFilterActive();
    }

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

    private void OnSearchTextChanged(string value) => RaiseFiltersChanged(FilterChangeKind.ImmediateApply);

    private void OnUseNowChanged(bool value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    private void OnSelectedDateChanged(DateTime value)
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

    private void OnSelectedTimeChanged(TimeSpan value)
    {
        if (_isUpdatingDateTimeSelection)
        {
            return;
        }

        RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    }

    private void OnIsGroupRoomSelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    private void OnIsSilentStudySelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    private void OnIsNoReservationSelectedChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    private void OnRequireFreeWifiChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    private void OnRequirePowerOutletsChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);
    private void OnRequireWhiteboardChanged(bool value) => RaiseFiltersChanged(FilterChangeKind.PreviewOnly);

    private void OnMinimumOpenHoursChanged(double value)
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

    private void OnSelectedSortOptionChanged(string value)
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
