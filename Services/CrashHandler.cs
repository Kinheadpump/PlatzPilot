using System.Net.Http;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using PlatzPilot.Messages;
using PlatzPilot.Resources.Strings;

namespace PlatzPilot.Services;

public static class CrashHandler
{
    private const string CrashReportOptOutKey = "CrashReportOptOut";
    private const string CrashFileName = "last_crash.txt";

    public static void Initialize()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static async Task CheckForCrashReportAsync(string discordWebhookUrl)
    {
        var filePath = Path.Combine(FileSystem.Current.AppDataDirectory, CrashFileName);
        if (!File.Exists(filePath))
        {
            return;
        }

        var crashText = await File.ReadAllTextAsync(filePath);

        if (Preferences.Default.Get(CrashReportOptOutKey, false))
        {
            TryDeleteCrashFile(filePath);
            return;
        }

        var result = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Shell.Current is null)
            {
                return null;
            }

            return await Shell.Current.DisplayActionSheetAsync(
                AppResources.CrashReportTitle,
                AppResources.CrashReportNo,
                null,
                AppResources.CrashReportSend,
                AppResources.CrashReportNeverAsk);
        });

        if (result == AppResources.CrashReportNeverAsk)
        {
            // 1. Opt-Out in den Preferences setzen
            Preferences.Default.Set(CrashReportOptOutKey, true);

            // 2. Datei GARANTIERT löschen
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // 3. UI benachrichtigen, dass sich die Einstellung geändert hat
            WeakReferenceMessenger.Default.Send(new CrashReportSettingsChangedMessage());
            return;
        }

        try
        {
            if (result == AppResources.CrashReportSend)
            {
                await SendToDiscordAsync(discordWebhookUrl, crashText);
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleException(exception);
            return;
        }

        HandleException(new Exception($"Unhandled exception object: {e.ExceptionObject}"));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception);
        e.SetObserved();
    }

    private static void HandleException(Exception exception)
    {
        if (Preferences.Default.Get(CrashReportOptOutKey, false))
        {
            return;
        }

        try
        {
            var payload = BuildCrashReport(exception);
            var filePath = Path.Combine(FileSystem.Current.AppDataDirectory, CrashFileName);
            File.WriteAllText(filePath, payload);
        }
        catch
        {
            // Swallow any exception to avoid cascading crashes during shutdown.
        }
    }

    private static string BuildCrashReport(Exception exception)
    {
        var payload = new
        {
            embeds = new[]
            {
                new
                {
                    title = "🚨 PlatzPilot Crash Report",
                    description = $"**Stack Trace:**\n```text\n{Truncate(exception.StackTrace ?? "No stack trace", 4000)}\n```",
                    color = 16711680,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    fields = new[]
                    {
                        new { name = "App Version", value = AppInfo.Current.VersionString, inline = true },
                        new { name = "OS", value = $"{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}", inline = true },
                        new { name = "Device", value = $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}", inline = true },
                        new { name = "Exception Type", value = exception.GetType().FullName ?? "Unknown", inline = false },
                        new { name = "Message", value = exception.Message, inline = false }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value.Substring(0, maxLength);
    }

    private static async Task SendToDiscordAsync(string discordWebhookUrl, string crashText)
    {
        using var httpClient = new HttpClient();
        using var content = new StringContent(crashText, Encoding.UTF8, "application/json");
        await httpClient.PostAsync(discordWebhookUrl, content);
    }

    private static void TryDeleteCrashFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Swallow to keep startup resilient.
        }
    }
}
