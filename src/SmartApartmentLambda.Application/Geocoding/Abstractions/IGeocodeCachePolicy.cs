namespace SmartApartmentLambda.Application.Geocoding;

public interface IGeocodeCachePolicy
{
    TimeSpan CacheDuration { get; }
}
