using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApartmentLambda.Service;

public interface IStartup
{
    IServiceCollection Services { get; }
    void ConfigureServices(IConfiguration configuration);
}