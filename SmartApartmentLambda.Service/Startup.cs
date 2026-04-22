using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApartmentLambda.Service;

public class Startup : IStartup
{
    public IServiceCollection Services { get; set; } = null!;

    public void ConfigureServices(IConfiguration configuration)
    {
        Services = new ServiceCollection()
            .AddLogging();
    }
}