namespace SmartApartmentLambda.Application.Geocoding;

public interface IGoogleGeocodingClient
{
    Task<GoogleGeocodingResponse> GeocodeAsync(string address, CancellationToken cancellationToken);
}
