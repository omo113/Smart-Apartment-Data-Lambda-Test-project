using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Infrastructure.Configuration;

namespace SmartApartmentLambda.Infrastructure.Geocoding;

public sealed class GoogleGeocodingClient : IGoogleGeocodingClient
{
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
        var requestUri = $"{_options.GeocodePath}?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException(
                    $"Google Geocoding returned HTTP {(int)response.StatusCode}.");
            }

            var googleStatus = ExtractStatus(responseBody);

            _logger.LogInformation(
                "Received Google geocoding response with status {GoogleStatus}",
                googleStatus);

            return new GoogleGeocodingResponse(responseBody, googleStatus);
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
    }

    private static string ExtractStatus(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("status", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(statusElement.GetString()))
            {
                throw new UpstreamServiceException("The Google Geocoding response did not contain a valid status value.");
            }

            return statusElement.GetString()!;
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
}
