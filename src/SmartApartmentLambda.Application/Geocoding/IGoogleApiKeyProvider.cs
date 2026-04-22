namespace SmartApartmentLambda.Application.Geocoding;

public interface IGoogleApiKeyProvider
{
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken);
}
