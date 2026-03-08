using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using PlatzPilot.Messages;
using PlatzPilot.Services;
using PlatzPilot.ViewModels;

namespace PlatzPilot.Tests;

public sealed class MainPageViewModelTests
{
    [Fact]
    public void CompleteOnboardingCommand_PersistsState_HidesOnboardingAndTriggersLoad()
    {
        WeakReferenceMessenger.Default.Reset();

        var karlsruhe = TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1");
        var stuttgart = TestConfigFactory.CreateCity("stuttgart", "Stuttgart", "S1");
        var config = TestConfigFactory.Create(karlsruhe, stuttgart);
        var preferences = new TestPreferencesService();
        var navigationService = new StubNavigationService();

        var filters = new FilterViewModel(config, preferences);
        var navigation = new NavigationViewModel(config, preferences);
        var settings = new SettingsViewModel(config, navigationService, preferences);

        var handler = new TrackingHttpMessageHandler(TestPayloads.ValidJsonp);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var seatFinderService = new SeatFinderService(
            new StubHttpClientFactory(httpClient),
            config,
            preferences,
            NullLogger<SeatFinderService>.Instance);

        var safeArrivalService = new SafeArrivalForecastService(config);
        var mensaForecastService = new MensaForecastService(config, safeArrivalService);
        var featureService = new StubStudySpaceFeatureService();

        var seatList = new SeatListViewModel(
            seatFinderService,
            safeArrivalService,
            featureService,
            navigationService,
            mensaForecastService,
            preferences,
            filters,
            navigation,
            settings,
            config);

        var viewModel = new MainPageViewModel(
            seatList,
            filters,
            navigation,
            settings,
            config,
            preferences);

        viewModel.CompleteOnboardingCommand.Execute(null);

        Assert.False(viewModel.IsOnboardingVisible);
        Assert.Contains(preferences.SetCalls,
            call => call.Key == "HasCompletedOnboarding" && call.Value is bool value && value);
        Assert.NotNull(viewModel.SeatList.LoadSpacesCommand.ExecutionTask);
    }

    [Fact]
    public async Task CityChangedMessage_UpdatesSelectedCityFromPreferences()
    {
        WeakReferenceMessenger.Default.Reset();
        using var _ = DispatcherQueueScope.TryCreate();

        var karlsruhe = TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1");
        var stuttgart = TestConfigFactory.CreateCity("stuttgart", "Stuttgart", "S1");
        var config = TestConfigFactory.Create(karlsruhe, stuttgart);
        var preferences = new TestPreferencesService { SelectedCityId = karlsruhe.Id };
        var navigationService = new StubNavigationService();

        var filters = new FilterViewModel(config, preferences);
        var navigation = new NavigationViewModel(config, preferences);
        var settings = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(settings);

        var handler = new TrackingHttpMessageHandler(TestPayloads.ValidJsonp);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var seatFinderService = new SeatFinderService(
            new StubHttpClientFactory(httpClient),
            config,
            preferences,
            NullLogger<SeatFinderService>.Instance);
        var safeArrivalService = new SafeArrivalForecastService(config);
        var mensaForecastService = new MensaForecastService(config, safeArrivalService);
        var featureService = new StubStudySpaceFeatureService();

        var seatList = new SeatListViewModel(
            seatFinderService,
            safeArrivalService,
            featureService,
            navigationService,
            mensaForecastService,
            preferences,
            filters,
            navigation,
            settings,
            config);
        WeakReferenceMessenger.Default.UnregisterAll(seatList);

        var viewModel = new MainPageViewModel(
            seatList,
            filters,
            navigation,
            settings,
            config,
            preferences);

        preferences.SelectedCityId = stuttgart.Id;
        WeakReferenceMessenger.Default.Send(new CityChangedMessage());

        var updated = await TestAsyncHelper.WaitForConditionAsync(
            () => viewModel.SelectedCity?.Id == stuttgart.Id,
            TimeSpan.FromSeconds(1));

        Assert.True(updated);
    }
}
