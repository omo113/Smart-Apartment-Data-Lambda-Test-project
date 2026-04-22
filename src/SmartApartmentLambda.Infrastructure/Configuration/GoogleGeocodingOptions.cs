using System.ComponentModel.DataAnnotations;

namespace SmartApartmentLambda.Infrastructure.Configuration;

public sealed class GoogleGeocodingOptions
{
    public const string SectionName = "GoogleGeocoding";

    [Required]
    public string BaseUrl { get; init; } = "https://geocode.googleapis.com";

    [Required]
    public string GeocodePath { get; init; } = "/v4/geocode/address";

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;

    [Required]
    public string ApiKeyEnvironmentVariableName { get; init; } = "GOOGLE_GEOCODING_API_KEY";

    public string? ApiKeySecretName { get; init; }

    public string? ApiKeySecretJsonKey { get; init; } = "apiKey";
}
