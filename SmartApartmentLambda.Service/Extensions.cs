using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartApartmentLambda.Service;

public static class Extensions
{
    public static Bootstrapper BuildConfiguration(this Bootstrapper bootstrapper)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        bootstrapper.Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();
        return bootstrapper;
    }

    public static Bootstrapper Use<TStartup>(this Bootstrapper bootstrapper) where TStartup : IStartup, new()
    {
        bootstrapper.Startup = new TStartup();
        bootstrapper.Startup.ConfigureServices(bootstrapper.Configuration);
        return bootstrapper;
    }

    public static IServiceProvider Run(this Bootstrapper bootstrapper)
    {
        return bootstrapper.Startup.Services.BuildServiceProvider();
    }
}
