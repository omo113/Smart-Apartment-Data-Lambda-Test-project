using SmartApartmentLambda.Application.Geocoding.Caching;

namespace SmartApartmentLambda.Application.Geocoding.Abstractions;

public interface IGeocodeCacheRepository
{
    Task<GeocodeCacheEntry?> GetAsync(string normalizedAddress, CancellationToken cancellationToken);
    Task SaveAsync(GeocodeCacheEntry entry, CancellationToken cancellationToken);
}
