using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Infrastructure.Configuration;

namespace SmartApartmentLambda.Infrastructure.Geocoding;

public sealed class GoogleApiKeyProvider : IGoogleApiKeyProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleApiKeyProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly GoogleGeocodingOptions _options;
    private readonly IAmazonSecretsManager _secretsManager;
    private string? _cachedApiKey;

    public GoogleApiKeyProvider(
        IAmazonSecretsManager secretsManager,
        IOptions<GoogleGeocodingOptions> options,
        IConfiguration configuration,
        ILogger<GoogleApiKeyProvider> logger)
    {
        _secretsManager = secretsManager;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_cachedApiKey))
        {
            return _cachedApiKey;
        }

        var environmentOverride = _configuration[_options.ApiKeyEnvironmentVariableName];
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            _cachedApiKey = environmentOverride.Trim();
            return _cachedApiKey;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKeySecretName))
        {
            throw new InternalServiceException(
                $"No Google API key was configured. Set environment variable '{_options.ApiKeyEnvironmentVariableName}' or provide GoogleGeocoding:ApiKeySecretName.");
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedApiKey))
            {
                return _cachedApiKey;
            }

            var response = await _secretsManager.GetSecretValueAsync(
                new GetSecretValueRequest
                {
                    SecretId = _options.ApiKeySecretName
                },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response.SecretString))
            {
                throw new InternalServiceException("The configured Google API key secret was empty.");
            }

            _cachedApiKey = ExtractApiKey(response.SecretString);
            _logger.LogInformation("Loaded Google Geocoding API key from Secrets Manager");

            return _cachedApiKey;
        }
        catch (InternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InternalServiceException("Failed to load the Google API key from Secrets Manager.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private string ExtractApiKey(string secretString)
    {
        if (!secretString.TrimStart().StartsWith('{'))
        {
            return secretString.Trim();
        }

        try
        {
            using var document = JsonDocument.Parse(secretString);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InternalServiceException("The configured Google API key secret must be a JSON object or raw string.");
            }

            var propertyName = string.IsNullOrWhiteSpace(_options.ApiKeySecretJsonKey)
                ? "apiKey"
                : _options.ApiKeySecretJsonKey;

            if (!document.RootElement.TryGetProperty(propertyName, out var apiKeyProperty) ||
                apiKeyProperty.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(apiKeyProperty.GetString()))
            {
                throw new InternalServiceException(
                    $"The configured Google API key secret did not contain the expected '{propertyName}' property.");
            }

            return apiKeyProperty.GetString()!.Trim();
        }
        catch (JsonException ex)
        {
            throw new InternalServiceException("The configured Google API key secret contains invalid JSON.", ex);
        }
    }
}
