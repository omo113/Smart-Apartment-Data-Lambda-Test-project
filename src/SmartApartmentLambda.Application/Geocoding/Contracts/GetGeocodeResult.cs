using SmartApartmentLambda.Application.Geocoding.Caching;

namespace SmartApartmentLambda.Application.Geocoding.Contracts;

public sealed record GetGeocodeResult(
    string ResponseBody,
    GeocodeCacheStatus CacheStatus,
    string GoogleStatus);
