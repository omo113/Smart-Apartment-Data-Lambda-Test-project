using SmartApartmentLambda.Application.Geocoding.Contracts;

namespace SmartApartmentLambda.Application.Geocoding.Abstractions;

public interface IGoogleGeocodingClient
{
    Task<GoogleGeocodingResponse> GeocodeAsync(string address, CancellationToken cancellationToken);
}
