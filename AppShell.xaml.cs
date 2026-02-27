using PlatzPilot.Views;

namespace PlatzPilot;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        Routing.RegisterRoute("MainPage", typeof(MainPage));
        Routing.RegisterRoute("DetailPage", typeof(DetailPage));
    }
}