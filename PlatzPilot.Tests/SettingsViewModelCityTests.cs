using CommunityToolkit.Mvvm.Messaging;
using PlatzPilot.Messages;
using PlatzPilot.ViewModels;

namespace PlatzPilot.Tests;

public sealed class SettingsViewModelCityTests
{
    [Fact]
    public void SelectedCity_PersistsAndSendsMessage()
    {
        WeakReferenceMessenger.Default.Reset();

        var karlsruhe = TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1");
        var berlin = TestConfigFactory.CreateCity("berlin", "Berlin", "B1");
        var config = TestConfigFactory.Create(karlsruhe, berlin);
        var preferences = new TestPreferencesService();
        var navigationService = new StubNavigationService();

        var viewModel = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(viewModel);

        CityChangedMessage? received = null;
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<CityChangedMessage>(recipient, (_, message) => received = message);

        viewModel.SelectedCity = berlin;

        Assert.Equal(berlin.Id, preferences.SelectedCityId);
        Assert.NotNull(received);

        WeakReferenceMessenger.Default.UnregisterAll(recipient);
    }

    [Fact]
    public void CampusSouthProperties_ReflectKarlsruheSelection()
    {
        WeakReferenceMessenger.Default.Reset();

        var karlsruhe = TestConfigFactory.CreateCity("karlsruhe", "Karlsruhe", "L1");
        var berlin = TestConfigFactory.CreateCity("berlin", "Berlin", "B1");
        var config = TestConfigFactory.Create(karlsruhe, berlin);
        var preferences = new TestPreferencesService { SelectedCityId = karlsruhe.Id };
        var navigationService = new StubNavigationService();

        var viewModel = new SettingsViewModel(config, navigationService, preferences);
        WeakReferenceMessenger.Default.UnregisterAll(viewModel);

        Assert.True(viewModel.IsKarlsruheSelected);
        Assert.Equal(1.0, viewModel.CampusSouthOpacity);

        viewModel.SelectedCity = berlin;

        Assert.False(viewModel.IsKarlsruheSelected);
        Assert.Equal(0.4, viewModel.CampusSouthOpacity);
        Assert.Contains("Nur Karlsruhe", viewModel.CampusSouthLabel);
    }
}
