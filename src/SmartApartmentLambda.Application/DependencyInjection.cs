using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartApartmentLambda.Application.Geocoding;

namespace SmartApartmentLambda.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetGeocodeQuery>());
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
