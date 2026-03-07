using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using PlatzPilot.Messages;
using PlatzPilot.Views;

namespace PlatzPilot;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_serviceProvider.GetRequiredService<AppShell>());
        window.Resumed += (_, _) => WeakReferenceMessenger.Default.Send(new AppResumedMessage());
        return window;
    }
}
