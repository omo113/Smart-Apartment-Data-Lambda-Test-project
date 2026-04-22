namespace SmartApartmentLambda.Application.Geocoding;

public sealed record GetGeocodeResult(
    string ResponseBody,
    GeocodeCacheStatus CacheStatus,
    string GoogleStatus);
