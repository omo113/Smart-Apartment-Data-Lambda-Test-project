using MediatR;
using SmartApartmentLambda.Application.Geocoding.Contracts;

namespace SmartApartmentLambda.Application.Queries;

public sealed record GetGeocodeQuery(string Address) : IRequest<GetGeocodeResult>;
