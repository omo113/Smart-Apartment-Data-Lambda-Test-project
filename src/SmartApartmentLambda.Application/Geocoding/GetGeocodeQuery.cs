using MediatR;

namespace SmartApartmentLambda.Application.Geocoding;

public sealed record GetGeocodeQuery(string Address) : IRequest<GetGeocodeResult>;
