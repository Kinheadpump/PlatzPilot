using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PlatzPilot.Configuration;
using PlatzPilot.Services;

namespace PlatzPilot.Tests;

public sealed class SeatFinderServiceTests
{
    [Fact]
    public async Task FetchSeatDataAsync_ValidJsonp_ParsesPayload()
    {
        var config = CreateSeatFinderConfig();
        var json = """
                   [
                     { "seatestimate": {}, "manualcount": {}, "location": {} },
                     { "location": { "L1": [ { "name": "Library", "long_name": "Library L1", "available_seats": 42 } ] } }
                   ]
                   """;
        var jsonp = $"PlatzPilot_123({json})";

        using var client = new HttpClient(new StubHttpMessageHandler(jsonp))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var service = new SeatFinderService(new StubHttpClientFactory(client), config, NullLogger<SeatFinderService>.Instance);

        var result = await service.FetchSeatDataAsync(limit: 1);

        Assert.Single(result);
        Assert.Equal("Library L1", result[0].Name);
        Assert.Equal(42, result[0].TotalSeats);
    }

    [Fact]
    public async Task FetchSeatDataAsync_MalformedJsonp_ThrowsInvalidOperationException()
    {
        var config = CreateSeatFinderConfig();
        var jsonp = "PlatzPilot_123{not-jsonp}";

        using var client = new HttpClient(new StubHttpMessageHandler(jsonp))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var service = new SeatFinderService(new StubHttpClientFactory(client), config, NullLogger<SeatFinderService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchSeatDataAsync(limit: 1));
    }

    private static AppConfig CreateSeatFinderConfig()
    {
        return new AppConfig
        {
            SeatFinder = new SeatFinderConfig
            {
                BaseUrl = "https://example.test/seatfinder",
                NowToken = "now",
                Locations = ["L1"],
                CallbackPrefix = "PlatzPilot_",
                LocationSeparator = ",",
                QueryStartSeparator = "?",
                QueryPairSeparator = "=",
                QueryParameterSeparator = "&",
                RequestTimeoutSeconds = 1,
                JsonpMinBlocks = 2,
                Query = new SeatFinderQueryConfig
                {
                    CallbackParam = "callback",
                    TimestampParam = "_",
                    Location0Param = "location[0]",
                    Values0Param = "values[0]",
                    After0Param = "after[0]",
                    Before0Param = "before[0]",
                    Limit0Param = "limit[0]",
                    Location1Param = "location[1]",
                    Values1Param = "values[1]",
                    After1Param = "after[1]",
                    Before1Param = "before[1]",
                    Limit1Param = "limit[1]",
                    Values0Value = "seatestimate,manualcount",
                    Values1Value = "location"
                }
            },
            Internal = new InternalConfig
            {
                JsonpParseErrorText = "Parse error",
                HttpRequestErrorFormat = "Request error: {0}"
            }
        };
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StubHttpMessageHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
