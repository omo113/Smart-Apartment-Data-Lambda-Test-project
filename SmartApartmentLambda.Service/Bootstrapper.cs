using Microsoft.Extensions.Configuration;

namespace SmartApartmentLambda.Service;

public class Bootstrapper
{
    public IConfigurationRoot Configuration;

    public IStartup Startup;

    public static Bootstrapper CreateBootstrapper()
    {
        return new Bootstrapper();
    }
}