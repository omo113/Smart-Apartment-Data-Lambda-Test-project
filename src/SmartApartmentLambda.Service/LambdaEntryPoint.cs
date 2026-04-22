using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SmartApartmentLambda.Service;

// The AWS Mock Lambda Test Tool expects a class-library style handler string.
// This wrapper keeps local tooling happy while the deployed Lambda can still
// use the executable assembly bootstrap defined in Program.cs.
public sealed class LambdaEntryPoint
{
    private static readonly Lazy<IHost> Host = new(CreateHost, LazyThreadSafetyMode.ExecutionAndPublication);

    public Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var function = Host.Value.Services.GetRequiredService<Function>();
        return function.FunctionHandlerAsync(request, context);
    }

    private static IHost CreateHost()
    {
        return LambdaHostBootstrap.BuildHost(Array.Empty<string>());
    }
}
