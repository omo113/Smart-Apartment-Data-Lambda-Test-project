namespace SmartApartmentLambda.Application.Geocoding;

public sealed record GoogleGeocodingResponse(
    string ResponseBody,
    string Status);
