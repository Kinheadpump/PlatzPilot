using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using PlatzPilot.Messages;
using PlatzPilot.Models;
using PlatzPilot.Services;
using PlatzPilot.ViewModels;

namespace PlatzPilot.Tests;

public sealed class SeatListViewModelTests
{
    [Fact]
    public async Task LoadSpacesAsync_OnboardingIncomplete_ReturnsEarlyWithoutSeatFinderCall()
    {
        WeakReferenceMessenger.Default.Reset();

        var config = TestConfigFactory.Create(TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1"));
        var preferences = new TestPreferencesService();
        preferences.Set(config.Preferences.OnboardingCompletedKey, false);

        var handler = new TrackingHttpMessageHandler(TestPayloads.ValidJsonp);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var seatFinderService = new SeatFinderService(
            new StubHttpClientFactory(httpClient),
            config,
            preferences,
            NullLogger<SeatFinderService>.Instance);

        var navigationService = new StubNavigationService();
        var safeArrivalService = new SafeArrivalForecastService(config);
        var mensaForecastService = new MensaForecastService(config, safeArrivalService);
        var featureService = new StubStudySpaceFeatureService();
        var filters = new FilterViewModel(config, preferences);
        var navigation = new NavigationViewModel(config, preferences);
        var settings = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(settings);

        var viewModel = new SeatListViewModel(
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

        await viewModel.LoadSpacesAsync();

        Assert.Equal(0, handler.CallCount);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task CityChangedMessage_ResetsCachesAndSignalsSwitch()
    {
        WeakReferenceMessenger.Default.Reset();
        using var _ = DispatcherQueueScope.TryCreate();

        var config = TestConfigFactory.Create(TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1"));
        var preferences = new TestPreferencesService();
        preferences.Set(config.Preferences.OnboardingCompletedKey, false);

        var handler = new TrackingHttpMessageHandler(TestPayloads.ValidJsonp);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var seatFinderService = new SeatFinderService(
            new StubHttpClientFactory(httpClient),
            config,
            preferences,
            NullLogger<SeatFinderService>.Instance);

        var navigationService = new StubNavigationService();
        var safeArrivalService = new SafeArrivalForecastService(config);
        var mensaForecastService = new MensaForecastService(config, safeArrivalService);
        var featureService = new StubStudySpaceFeatureService();
        var filters = new FilterViewModel(config, preferences);
        var navigation = new NavigationViewModel(config, preferences);
        var settings = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(settings);

        var viewModel = new SeatListViewModel(
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

        SeedCaches(viewModel);

        var switchingObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SeatListViewModel.IsSwitchingCity) && viewModel.IsSwitchingCity)
            {
                switchingObserved.TrySetResult(true);
            }
        };

        WeakReferenceMessenger.Default.Send(new CityChangedMessage());

        var switchingSeen = await TestAsyncHelper.WaitForConditionAsync(
            () => switchingObserved.Task.IsCompleted,
            TimeSpan.FromSeconds(1));
        Assert.True(switchingSeen);

        var cachesCleared = await TestAsyncHelper.WaitForConditionAsync(() =>
            GetPrivateField<List<StudySpace>>(viewModel, "_allSpaces").Count == 0 &&
            GetPrivateField<IReadOnlyDictionary<string, List<SeatHistoryPoint>>>(
                viewModel,
                "_historicalSeatDataByLocation").Count == 0 &&
            GetPrivateField<Dictionary<string, SafeArrivalRecommendation?>>(
                viewModel,
                "_spaceSafeArrivalCache").Count == 0 &&
            GetPrivateField<Dictionary<string, SafeArrivalRecommendation?>>(
                viewModel,
                "_buildingSafeArrivalCache").Count == 0 &&
            GetPrivateField<Dictionary<string, List<float>>>(
                viewModel,
                "_spaceChartSeriesCache").Count == 0 &&
            GetPrivateField<Dictionary<string, List<float>>>(
                viewModel,
                "_buildingChartSeriesCache").Count == 0 &&
            GetPrivateField<DateTime>(viewModel, "_lastLiveSnapshotFetchUtc") == DateTime.MinValue,
            TimeSpan.FromSeconds(1));

        Assert.True(cachesCleared);
    }

    [Fact]
    public async Task LoadSpacesAsync_CompletesAfterLivePhaseWhileHistoryLoadsInBackground()
    {
        WeakReferenceMessenger.Default.Reset();
        using var _ = DispatcherQueueScope.TryCreate();

        var config = TestConfigFactory.Create(TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1"));
        config.SeatFinder.LiveSnapshotPoints = 1;
        config.SeatFinder.WeeklyHistoryPoints = 99;
        config.SeatFinder.RequestTimeoutSeconds = 5;

        var preferences = new TestPreferencesService();
        preferences.Set(config.Preferences.OnboardingCompletedKey, true);

        var handler = new TwoPhaseTrackingHttpMessageHandler(
            TestPayloads.ValidJsonp,
            config.SeatFinder.LiveSnapshotPoints,
            config.SeatFinder.WeeklyHistoryPoints);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var seatFinderService = new SeatFinderService(
            new StubHttpClientFactory(httpClient),
            config,
            preferences,
            NullLogger<SeatFinderService>.Instance);

        var navigationService = new StubNavigationService();
        var safeArrivalService = new SafeArrivalForecastService(config);
        var mensaForecastService = new MensaForecastService(config, safeArrivalService);
        var featureService = new StubStudySpaceFeatureService();
        var filters = new FilterViewModel(config, preferences);
        var navigation = new NavigationViewModel(config, preferences);
        var settings = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(settings);

        var viewModel = new SeatListViewModel(
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

        var loadTask = viewModel.LoadSpacesAsync();

        var livePhaseCompleted = await TestAsyncHelper.WaitForConditionAsync(
            () => loadTask.IsCompleted &&
                  !viewModel.IsBusy &&
                  !viewModel.IsSwitchingCity &&
                  viewModel.UiLocations.Count > 0,
            TimeSpan.FromSeconds(1));

        Assert.True(livePhaseCompleted);
        Assert.Equal(1, handler.LiveCallCount);
        Assert.Single(viewModel.UiLocations);

        var weeklyStarted = await TestAsyncHelper.WaitForConditionAsync(
            () => handler.WeeklyCallCount == 1,
            TimeSpan.FromSeconds(1));

        Assert.True(weeklyStarted);
        Assert.False(GetPrivateField<bool>(viewModel, "_hasLoadedWeeklyHistory"));

        handler.ReleaseWeeklyResponse();

        var backgroundCompleted = await TestAsyncHelper.WaitForConditionAsync(
            () => GetPrivateField<bool>(viewModel, "_hasLoadedWeeklyHistory"),
            TimeSpan.FromSeconds(1));

        Assert.True(backgroundCompleted);
    }

    private static void SeedCaches(SeatListViewModel viewModel)
    {
        var spaces = new List<StudySpace>
        {
            new StudySpace { Id = "S1", Name = "Space 1", TotalSeats = 10 }
        };
        var history = new Dictionary<string, List<SeatHistoryPoint>>(StringComparer.OrdinalIgnoreCase)
        {
            ["S1"] =
            [
                new SeatHistoryPoint
                {
                    Timestamp = DateTime.UtcNow,
                    FreeSeats = 5,
                    OccupiedSeats = 5,
                    IsManualCount = false
                }
            ]
        };
        var safeArrival = new Dictionary<string, SafeArrivalRecommendation?>(StringComparer.OrdinalIgnoreCase)
        {
            ["S1"] = new SafeArrivalRecommendation { HasRecommendation = true }
        };
        var chartSeries = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase)
        {
            ["S1"] = [0.1f, 0.2f]
        };

        SetPrivateField(viewModel, "_allSpaces", spaces);
        SetPrivateField(viewModel, "_historicalSeatDataByLocation", history);
        SetPrivateField(viewModel, "_spaceSafeArrivalCache", safeArrival);
        SetPrivateField(viewModel, "_buildingSafeArrivalCache", new Dictionary<string, SafeArrivalRecommendation?>(safeArrival));
        SetPrivateField(viewModel, "_spaceChartSeriesCache", chartSeries);
        SetPrivateField(viewModel, "_buildingChartSeriesCache", new Dictionary<string, List<float>>(chartSeries));
        SetPrivateField(viewModel, "_lastLiveSnapshotFetchUtc", DateTime.UtcNow);
    }

    private static void SetPrivateField<T>(SeatListViewModel viewModel, string fieldName, T value)
    {
        var field = typeof(SeatListViewModel).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(viewModel, value);
    }

    private static T GetPrivateField<T>(SeatListViewModel viewModel, string fieldName)
    {
        var field = typeof(SeatListViewModel).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var value = field?.GetValue(viewModel);
        return value is T typed ? typed : default!;
    }
}
