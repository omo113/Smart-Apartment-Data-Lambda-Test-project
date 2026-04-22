using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding.Abstractions;
using SmartApartmentLambda.Application.Geocoding.Contracts;
using SmartApartmentLambda.Application.Geocoding.Errors;
using SmartApartmentLambda.Infrastructure.Configuration;
using System.Text.Json;
using Polly.RateLimiting;

namespace SmartApartmentLambda.Infrastructure.Geocoding.Clients;

public sealed class GoogleGeocodingClient : IGoogleGeocodingClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly IGoogleApiKeyProvider _googleApiKeyProvider;
    private readonly ILogger<GoogleGeocodingClient> _logger;
    private readonly GoogleGeocodingOptions _options;

    public GoogleGeocodingClient(
        HttpClient httpClient,
        IGoogleApiKeyProvider googleApiKeyProvider,
        IOptions<GoogleGeocodingOptions> options,
        ILogger<GoogleGeocodingClient> logger)
    {
        _httpClient = httpClient;
        _googleApiKeyProvider = googleApiKeyProvider;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<GoogleGeocodingResponse> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var apiKey = await _googleApiKeyProvider.GetApiKeyAsync(cancellationToken);
        var geocodePath = _options.GeocodePath.TrimEnd('/');
        var requestUri = $"{geocodePath}/{Uri.EscapeDataString(address)}?key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException(
                    $"Google Geocoding returned HTTP {(int)response.StatusCode}.");
            }

            var parsedResponse = ParseResponse(responseBody);

            _logger.LogInformation(
                "Received Google geocoding response with status {GoogleStatus} and {ResultCount} result(s)",
                parsedResponse.GoogleStatus,
                parsedResponse.ResultCount);

            return new GoogleGeocodingResponse(responseBody, parsedResponse.GoogleStatus);
        }
        catch (UpstreamServiceException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpstreamServiceException("The Google Geocoding request timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("The Google Geocoding request failed.", ex);
        }
        catch (RateLimiterRejectedException ex)
        {
            throw new UpstreamServiceException("The Google Geocoding request rate limit was exceeded locally.", ex);
        }
    }

    private static ParsedGoogleGeocodingResponse ParseResponse(string responseBody)
    {
        try
        {
            var response = JsonSerializer.Deserialize<GoogleGeocodeApiResponse>(responseBody, SerializerOptions);
            if (response?.Results is null)
            {
                throw new UpstreamServiceException("The Google Geocoding response did not contain a valid results array.");
            }

            ValidateResults(response.Results);

            var googleStatus = response.Results.Count == 0 ? "ZERO_RESULTS" : "OK";
            return new ParsedGoogleGeocodingResponse(googleStatus, response.Results.Count);
        }
        catch (UpstreamServiceException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new UpstreamServiceException("The Google Geocoding response contained malformed JSON.", ex);
        }
    }

    private static void ValidateResults(IReadOnlyList<GoogleGeocodeResult> results)
    {
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];

            if (string.IsNullOrWhiteSpace(result.PlaceId))
            {
                throw new UpstreamServiceException(
                    $"The Google Geocoding response result at index {index} did not contain a valid placeId.");
            }

            if (string.IsNullOrWhiteSpace(result.FormattedAddress))
            {
                throw new UpstreamServiceException(
                    $"The Google Geocoding response result at index {index} did not contain a valid formattedAddress.");
            }

            if (result.Location is null ||
                !result.Location.Latitude.HasValue ||
                !result.Location.Longitude.HasValue ||
                !IsValidLatitude(result.Location.Latitude.Value) ||
                !IsValidLongitude(result.Location.Longitude.Value))
            {
                throw new UpstreamServiceException(
                    $"The Google Geocoding response result at index {index} did not contain a valid location.");
            }
        }
    }

    private static bool IsValidLatitude(double latitude) =>
        !double.IsNaN(latitude) &&
        !double.IsInfinity(latitude) &&
        latitude is >= -90 and <= 90;

    private static bool IsValidLongitude(double longitude) =>
        !double.IsNaN(longitude) &&
        !double.IsInfinity(longitude) &&
        longitude is >= -180 and <= 180;

    private sealed record ParsedGoogleGeocodingResponse(string GoogleStatus, int ResultCount);
}
