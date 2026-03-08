using Microsoft.Maui.ApplicationModel;

namespace PlatzPilot.Services;

internal static class MainThreadHelper
{
    public static void BeginInvoke(Action action)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
        catch
        {
            action();
        }
    }

    public static Task InvokeAsync(Action action)
    {
        try
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }
        catch
        {
            action();
            return Task.CompletedTask;
        }
    }

    public static Task InvokeAsync(Func<Task> action)
    {
        try
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }
        catch
        {
            return action();
        }
    }

    public static Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return MainThread.InvokeOnMainThreadAsync(action);
        }
        catch
        {
            return action();
        }
    }
}
