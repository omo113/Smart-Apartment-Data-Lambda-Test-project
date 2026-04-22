using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding.Abstractions;
using SmartApartmentLambda.Application.Geocoding.Errors;
using SmartApartmentLambda.Infrastructure.Configuration;
using SmartApartmentLambda.Infrastructure.Geocoding.Clients;
using System.Net;
using Xunit;

namespace SmartApartmentLambda.Tests.Geocoding;

public sealed class GoogleGeocodingClientTests
{
    [Fact]
    public async Task GeocodeAsync_UsesV4AddressEndpoint_AndReturnsOkStatus()
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CreateGoogleResultsResponse("ChIJxQvW8wK6j4AR3ukttGy3w2s"))
        });
        var client = CreateClient(handler);

        var response = await client.GeocodeAsync("1600 Amphitheatre Pkwy, Mountain View, CA", CancellationToken.None);

        Assert.Equal("OK", response.Status);
        Assert.Contains("\"formattedAddress\"", response.ResponseBody, StringComparison.Ordinal);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal(
            "https://geocode.googleapis.com/v4/geocode/address/1600%20Amphitheatre%20Pkwy%2C%20Mountain%20View%2C%20CA?key=test-api-key",
          handler.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GeocodeAsync_ReturnsZeroResultsStatus_WhenGoogleReturnsNoResults()
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "results": []
                }
                """)
        });
        var client = CreateClient(handler);

        var response = await client.GeocodeAsync("999 Unknown St", CancellationToken.None);

        Assert.Equal("ZERO_RESULTS", response.Status);
    }

    [Fact]
    public async Task GeocodeAsync_Throws_WhenResultDoesNotContainCoordinates()
    {
        var handler = new StubHttpMessageHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {
                  "results": [
                    {
                      "place": "//places.googleapis.com/places/test-place",
                      "placeId": "test-place",
                      "formattedAddress": "1600 Amphitheatre Pkwy, Mountain View, CA 94043, USA"
                    }
                  ]
                }
                """)
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<UpstreamServiceException>(() =>
            client.GeocodeAsync("1600 Amphitheatre Pkwy, Mountain View, CA", CancellationToken.None));

        Assert.Contains("valid location", exception.Message, StringComparison.Ordinal);
    }

    private static GoogleGeocodingClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://geocode.googleapis.com", UriKind.Absolute)
        };

        return new GoogleGeocodingClient(
            httpClient,
            new StubGoogleApiKeyProvider(),
            Options.Create(new GoogleGeocodingOptions()),
            NullLogger<GoogleGeocodingClient>.Instance);
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
              "viewport": {
                "low": {
                  "latitude": 37.4208865,
                  "longitude": -122.0871396
                },
                "high": {
                  "latitude": 37.4235844,
                  "longitude": -122.0844416
                }
              },
              "formattedAddress": "Google Building 41, 1600 Amphitheatre Pkwy, Mountain View, CA 94043, USA",
              "postalAddress": {
                "regionCode": "US",
                "languageCode": "en",
                "postalCode": "94043-1351",
                "administrativeArea": "CA",
                "locality": "Mountain View",
                "addressLines": [
                  "1600 Amphitheatre Pkwy"
                ]
              },
              "addressComponents": [
                {
                  "longText": "Google Building 41",
                  "shortText": "Google Building 41",
                  "types": [
                    "premise"
                  ],
                  "languageCode": "en"
                },
                {
                  "longText": "1600",
                  "shortText": "1600",
                  "types": [
                    "street_number"
                  ]
                }
              ],
              "types": [
                "premise",
                "street_address"
              ]
            }
          ]
        }
        """;

    private sealed class StubGoogleApiKeyProvider : IGoogleApiKeyProvider
    {
        public Task<string> GetApiKeyAsync(CancellationToken cancellationToken) => Task.FromResult("test-api-key");
    }

    private sealed class StubHttpMessageHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory = responseFactory;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_responseFactory());
        }
    }
}
