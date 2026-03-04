namespace PlatzPilot.ViewModels;

public sealed class MainPageViewModel(
    SeatListViewModel seatList,
    FilterViewModel filters,
    NavigationViewModel navigation,
    SettingsViewModel settings)
{

    public SeatListViewModel SeatList { get; } = seatList;
    public FilterViewModel Filters { get; } = filters;
    public NavigationViewModel Navigation { get; } = navigation;
    public SettingsViewModel Settings { get; } = settings;
}
