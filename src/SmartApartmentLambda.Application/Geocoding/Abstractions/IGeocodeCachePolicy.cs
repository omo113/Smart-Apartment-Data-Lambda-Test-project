namespace SmartApartmentLambda.Application.Geocoding.Abstractions;

public interface IGeocodeCachePolicy
{
    TimeSpan CacheDuration { get; }
}
