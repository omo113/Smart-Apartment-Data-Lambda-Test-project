using Microsoft.Extensions.Logging.Abstractions;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Application.Queries;
using Xunit;

namespace SmartApartmentLambda.Tests.Geocoding;

public sealed class GetGeocodeQueryHandlerTests
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 4, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ReturnsCachedResponse_WhenCacheEntryIsFresh()
    {
        var repository = new StubGeocodeCacheRepository
        {
            EntryToReturn = new GeocodeCacheEntry(
                "70 vanderbilt ave, new york, ny 10017, united states",
                "70 Vanderbilt Ave, New York, NY 10017, United States",
                CreateGoogleResultsResponse("cached"),
                "OK",
                FixedUtcNow.AddDays(-1),
                FixedUtcNow.AddDays(29),
                FixedUtcNow.AddDays(29).ToUnixTimeSeconds())
        };
        var googleClient = new StubGoogleGeocodingClient();
        var handler = CreateHandler(repository, googleClient);

        var result = await handler.Handle(
            new GetGeocodeQuery("70  Vanderbilt Ave, New York, NY 10017, United States"),
            CancellationToken.None);

        Assert.Equal(GeocodeCacheStatus.Hit, result.CacheStatus);
        Assert.Equal("OK", result.GoogleStatus);
        Assert.Contains("\"cached\"", result.ResponseBody, StringComparison.Ordinal);
        Assert.Equal(0, googleClient.CallCount);
        Assert.Equal("70 vanderbilt ave, new york, ny 10017, united states", repository.LastRequestedKey);
    }

    [Fact]
    public async Task Handle_RefreshesExpiredCacheEntry_WhenRecordIsPastExpiry()
    {
        var repository = new StubGeocodeCacheRepository
        {
            EntryToReturn = new GeocodeCacheEntry(
                "70 vanderbilt ave, new york, ny 10017, united states",
                "70 Vanderbilt Ave, New York, NY 10017, United States",
                "{\"status\":\"OK\",\"results\":[{\"place_id\":\"expired\"}]}",
                "OK",
                FixedUtcNow.AddDays(-31),
                FixedUtcNow.AddMinutes(-1),
                FixedUtcNow.AddMinutes(-1).ToUnixTimeSeconds())
        };
        var googleClient = new StubGoogleGeocodingClient
        {
            Response = new GoogleGeocodingResponse(
                CreateGoogleResultsResponse("fresh"),
                "OK")
        };
        var handler = CreateHandler(repository, googleClient);

        var result = await handler.Handle(
            new GetGeocodeQuery("70 Vanderbilt Ave, New York, NY 10017, United States"),
            CancellationToken.None);

        Assert.Equal(GeocodeCacheStatus.Refresh, result.CacheStatus);
        Assert.Equal(1, googleClient.CallCount);
        Assert.Single(repository.SavedEntries);
        Assert.Equal(FixedUtcNow.AddDays(30), repository.SavedEntries[0].ExpiresAtUtc);
        Assert.Contains("\"fresh\"", result.ResponseBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_SavesNewCacheEntry_OnCacheMiss()
    {
        var repository = new StubGeocodeCacheRepository();
        var googleClient = new StubGoogleGeocodingClient
        {
            Response = new GoogleGeocodingResponse(
                CreateGoogleResultsResponse("new"),
                "OK")
        };
        var handler = CreateHandler(repository, googleClient);

        var result = await handler.Handle(
            new GetGeocodeQuery("70 Vanderbilt Ave, New York, NY 10017, United States"),
            CancellationToken.None);

        Assert.Equal(GeocodeCacheStatus.Miss, result.CacheStatus);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal("70 vanderbilt ave, new york, ny 10017, united states", repository.SavedEntries[0].NormalizedAddress);
        Assert.Equal(FixedUtcNow.AddDays(30).ToUnixTimeSeconds(), repository.SavedEntries[0].TtlEpochSeconds);
    }

    [Fact]
    public async Task Handle_CachesZeroResultsResponse()
    {
        var repository = new StubGeocodeCacheRepository();
        var googleClient = new StubGoogleGeocodingClient
        {
            Response = new GoogleGeocodingResponse(
                CreateZeroResultsResponse(),
                "ZERO_RESULTS")
        };
        var handler = CreateHandler(repository, googleClient);

        var result = await handler.Handle(
            new GetGeocodeQuery("999 Unknown St, New York, NY 10001, United States"),
            CancellationToken.None);

        Assert.Equal("ZERO_RESULTS", result.GoogleStatus);
        Assert.Equal(1, repository.SaveCallCount);
        Assert.Equal("ZERO_RESULTS", repository.SavedEntries[0].GoogleStatus);
    }

    [Fact]
    public async Task Handle_DoesNotCacheNonCacheableGoogleStatuses()
    {
        var repository = new StubGeocodeCacheRepository();
        var googleClient = new StubGoogleGeocodingClient
        {
            Response = new GoogleGeocodingResponse(
                "{\"status\":\"OVER_QUERY_LIMIT\",\"results\":[]}",
                "OVER_QUERY_LIMIT")
        };
        var handler = CreateHandler(repository, googleClient);

        var result = await handler.Handle(
            new GetGeocodeQuery("70 Vanderbilt Ave, New York, NY 10017, United States"),
            CancellationToken.None);

        Assert.Equal(GeocodeCacheStatus.Miss, result.CacheStatus);
        Assert.Equal("OVER_QUERY_LIMIT", result.GoogleStatus);
        Assert.Equal(0, repository.SaveCallCount);
    }

    private static GetGeocodeQueryHandler CreateHandler(
        StubGeocodeCacheRepository repository,
        StubGoogleGeocodingClient googleClient)
    {
        return new GetGeocodeQueryHandler(
            repository,
            new StubGeocodeCachePolicy(),
            googleClient,
            NullLogger<GetGeocodeQueryHandler>.Instance,
            new StubTimeProvider(FixedUtcNow));
    }

        private static string CreateGoogleResultsResponse(string placeId) => $$"""
                {
                    "results": [
                        {
                            "place": "//places.googleapis.com/places/{{placeId}}",
                            "placeId": "{{placeId}}",
                            "location": {
                                "latitude": 37.4224119,
                                "longitude": -122.0855078
                            },
                            "granularity": "ROOFTOP",
                            "formattedAddress": "1600 Amphitheatre Pkwy, Mountain View, CA 94043, USA",
                            "addressComponents": [
                                {
                                    "longText": "1600",
                                    "shortText": "1600",
                                    "types": [
                                        "street_number"
                                    ]
                                }
                            ],
                            "types": [
                                "street_address"
                            ]
                        }
                    ]
                }
                """;

        private static string CreateZeroResultsResponse() => """
                {
                    "results": []
                }
                """;

    private sealed class StubGeocodeCacheRepository : IGeocodeCacheRepository
    {
        public GeocodeCacheEntry? EntryToReturn { get; init; }
        public string? LastRequestedKey { get; private set; }
        public int SaveCallCount { get; private set; }
        public List<GeocodeCacheEntry> SavedEntries { get; } = [];

        public Task<GeocodeCacheEntry?> GetAsync(string normalizedAddress, CancellationToken cancellationToken)
        {
            LastRequestedKey = normalizedAddress;
            return Task.FromResult(EntryToReturn);
        }

        public Task SaveAsync(GeocodeCacheEntry entry, CancellationToken cancellationToken)
        {
            SaveCallCount++;
            SavedEntries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubGoogleGeocodingClient : IGoogleGeocodingClient
    {
        public int CallCount { get; private set; }
        public GoogleGeocodingResponse Response { get; init; } = new(CreateGoogleResultsResponse("stub"), "OK");

        public Task<GoogleGeocodingResponse> GeocodeAsync(string address, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(Response);
        }
    }

    private sealed class StubGeocodeCachePolicy : IGeocodeCachePolicy
    {
        public TimeSpan CacheDuration => TimeSpan.FromDays(30);
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public StubTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
