namespace SmartApartmentLambda.Application.Geocoding.Abstractions;

public interface IGoogleApiKeyProvider
{
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken);
}
