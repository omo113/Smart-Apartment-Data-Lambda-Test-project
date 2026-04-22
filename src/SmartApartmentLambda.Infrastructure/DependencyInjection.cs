using Amazon.DynamoDBv2;
using Amazon.SecretsManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartApartmentLambda.Application.Geocoding.Abstractions;
using SmartApartmentLambda.Infrastructure.Configuration;
using SmartApartmentLambda.Infrastructure.Geocoding.Caching;
using SmartApartmentLambda.Infrastructure.Geocoding.Clients;
using SmartApartmentLambda.Infrastructure.Geocoding.Providers;
using System.Threading.RateLimiting;

namespace SmartApartmentLambda.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<GoogleGeocodingOptions>()
            .Bind(configuration.GetSection(GoogleGeocodingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<GeocodeCacheOptions>()
            .Bind(configuration.GetSection(GeocodeCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
        services.TryAddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());

        services.AddSingleton<IGoogleApiKeyProvider, GoogleApiKeyProvider>();
        services.AddSingleton<IGeocodeCachePolicy, GeocodeCachePolicy>();
        services.AddSingleton<IGeocodeCacheRepository, DynamoDbGeocodeCacheRepository>();

        services
            .AddHttpClient<IGoogleGeocodingClient, GoogleGeocodingClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GoogleGeocodingOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddStandardResilienceHandler(options =>
            {
                var rateLimit = configuration
                    .GetSection(GoogleGeocodingOptions.SectionName)
                    .GetValue<int?>(nameof(GoogleGeocodingOptions.MaxRequestsPerSecond))
                    ?? 25;
                var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = rateLimit,
                    TokensPerPeriod = rateLimit,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = rateLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });

                options.RateLimiter.RateLimiter = arguments =>
                    rateLimiter.AcquireAsync(1, arguments.Context.CancellationToken);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });

        return services;
    }
}
