namespace SmartApartmentLambda.Application.Geocoding.Caching;

public sealed record GeocodeCacheEntry(
    string NormalizedAddress,
    string OriginalAddress,
    string ResponseBody,
    string GoogleStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    long TtlEpochSeconds);
