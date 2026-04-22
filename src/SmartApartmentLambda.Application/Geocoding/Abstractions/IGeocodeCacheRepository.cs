namespace SmartApartmentLambda.Application.Geocoding;

public interface IGeocodeCacheRepository
{
    Task<GeocodeCacheEntry?> GetAsync(string normalizedAddress, CancellationToken cancellationToken);
    Task SaveAsync(GeocodeCacheEntry entry, CancellationToken cancellationToken);
}
