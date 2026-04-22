using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Infrastructure.Configuration;

namespace SmartApartmentLambda.Infrastructure.Geocoding;

public sealed class GeocodeCachePolicy : IGeocodeCachePolicy
{
    private readonly GeocodeCacheOptions _options;

    public GeocodeCachePolicy(IOptions<GeocodeCacheOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan CacheDuration => TimeSpan.FromDays(_options.CacheDurationDays);
}
