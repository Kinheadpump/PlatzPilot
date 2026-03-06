using PlatzPilot.Configuration;
using PlatzPilot.Models;
using PlatzPilot.Services;
using PlatzPilot.ViewModels;

namespace PlatzPilot.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ToggleColorBlindModeCommand_UpdatesPropertyAndPersists()
    {
        var config = new AppConfig();
        config.Preferences.ColorBlindModeKey = "colorBlindMode";

        var preferences = new FakePreferencesService();
        var navigation = new StubNavigationService();

        var viewModel = new SettingsViewModel(config, navigation, preferences);

        viewModel.ToggleColorBlindModeCommand.Execute(null);

        Assert.True(viewModel.IsColorBlindMode);
        Assert.Contains(preferences.SetCalls,
            call => call.Key == config.Preferences.ColorBlindModeKey && call.Value is bool value && value);
    }

    private sealed class FakePreferencesService : IPreferencesService
    {
        public List<(string Key, object? Value)> SetCalls { get; } = [];
        private readonly Dictionary<string, object?> _values = new();

        public T Get<T>(string key, T defaultValue)
        {
            if (_values.TryGetValue(key, out var value) && value is T typed)
            {
                return typed;
            }

            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            _values[key] = value;
            SetCalls.Add((key, value));
        }
    }

    private sealed class StubNavigationService : INavigationService
    {
        public Task NavigateToDetailAsync(UiLocation location) => Task.CompletedTask;
        public Task OpenUrlAsync(string url) => Task.CompletedTask;
    }
}
