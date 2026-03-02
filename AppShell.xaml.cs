using PlatzPilot.Configuration;
using PlatzPilot.Views;

namespace PlatzPilot;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        var routes = AppConfigProvider.Current.Internal;
        Routing.RegisterRoute(routes.MainPageRoute, typeof(MainPage));
        Routing.RegisterRoute(routes.DetailPageRoute, typeof(DetailPage));
    }
}
