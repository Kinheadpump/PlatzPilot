using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace PlatzPilot.Views;

public sealed class StartupPage : ContentPage
{
    public StartupPage()
    {
        var lightBackground = Colors.White;
        var darkBackground = Color.FromArgb("#1f1f1f");
        var lightText = Color.FromArgb("#4a4a4a");
        var darkText = Color.FromArgb("#cfcfcf");

        var indicator = new ActivityIndicator
        {
            IsRunning = true,
            WidthRequest = 32,
            HeightRequest = 32,
            Color = Color.FromArgb("#1f6feb")
        };

        var label = new Label
        {
            Text = "Lade Daten...",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center
        };

        ApplyTheme(Application.Current?.RequestedTheme ?? AppTheme.Unspecified);
        Application.Current?.RequestedThemeChanged += (_, args) => ApplyTheme(args.RequestedTheme);

        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        indicator,
                        label
                    }
                }
            }
        };

        void ApplyTheme(AppTheme theme)
        {
            var useDark = theme == AppTheme.Dark;
            BackgroundColor = useDark ? darkBackground : lightBackground;
            label.TextColor = useDark ? darkText : lightText;
        }
    }
}
