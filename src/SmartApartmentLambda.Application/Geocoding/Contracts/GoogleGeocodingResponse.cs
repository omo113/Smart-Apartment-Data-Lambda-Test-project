namespace SmartApartmentLambda.Application.Geocoding.Contracts;

public sealed record GoogleGeocodingResponse(
    string ResponseBody,
    string Status);
