using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PlatzPilot.Constants;
using PlatzPilot.Models;
using PlatzPilot.Services;

namespace PlatzPilot.Tests;

internal sealed class TestPreferencesService : IPreferencesService
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Key, object? Value)> SetCalls { get; } = new();

    public string SelectedCityId
    {
        get => Get("SelectedCityId", CityIds.Karlsruhe);
        set => Set("SelectedCityId", value);
    }

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

internal sealed class StubNavigationService : INavigationService
{
    public Task NavigateToDetailAsync(UiLocation location) => Task.CompletedTask;
    public Task OpenUrlAsync(string url) => Task.CompletedTask;
}

internal sealed class StubStudySpaceFeatureService : IStudySpaceFeatureService
{
    public Task<IReadOnlyDictionary<string, StudySpaceFeatureEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, StudySpaceFeatureEntry> empty =
            new Dictionary<string, StudySpaceFeatureEntry>(StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(empty);
    }
}

internal sealed class TrackingHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;

    public TrackingHttpMessageHandler(string responseBody)
    {
        _responseBody = responseBody;
    }

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client;

    public StubHttpClientFactory(HttpClient client)
    {
        _client = client;
    }

    public HttpClient CreateClient(string name) => _client;
}

internal sealed class DispatcherQueueScope : IDisposable
{
    private readonly object? _controller;
    private readonly MethodInfo? _shutdownQueueAsync;

    private DispatcherQueueScope(object controller, MethodInfo shutdownQueueAsync)
    {
        _controller = controller;
        _shutdownQueueAsync = shutdownQueueAsync;
    }

    public static DispatcherQueueScope? TryCreate()
    {
        try
        {
            var controllerType = Type.GetType(
                "Windows.System.DispatcherQueueController, Windows, ContentType=WindowsRuntime");
            if (controllerType == null)
            {
                return null;
            }

            var createMethod = controllerType.GetMethod(
                "CreateOnCurrentThread",
                BindingFlags.Public | BindingFlags.Static);
            if (createMethod == null)
            {
                return null;
            }

            var controller = createMethod.Invoke(null, null);
            if (controller == null)
            {
                return null;
            }

            var shutdownQueueAsync = controllerType.GetMethod(
                "ShutdownQueueAsync",
                BindingFlags.Public | BindingFlags.Instance);
            if (shutdownQueueAsync == null)
            {
                return null;
            }

            return new DispatcherQueueScope(controller, shutdownQueueAsync);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_controller == null || _shutdownQueueAsync == null)
        {
            return;
        }

        try
        {
            var asyncOp = _shutdownQueueAsync.Invoke(_controller, null);
            if (asyncOp == null)
            {
                return;
            }

            var asTask = asyncOp.GetType().GetMethod("AsTask", Type.EmptyTypes);
            var task = asTask?.Invoke(asyncOp, null) as Task;
            task?.GetAwaiter().GetResult();
        }
        catch
        {
        }
    }
}

internal static class TestAsyncHelper
{
    public static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopAt = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stopAt)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }
}

internal static class TestPayloads
{
    public const string ValidJsonp = "PlatzPilot_123(["
        + "{ \"seatestimate\": {}, \"manualcount\": {}, \"location\": {} },"
        + "{ \"location\": { \"L1\": [ { \"name\": \"Library\", \"long_name\": \"Library L1\", \"available_seats\": 42 } ] } }"
        + "])";
}
