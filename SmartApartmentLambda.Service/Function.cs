using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SmartApartmentLambda.Service;

public class Function
{
    protected ILogger<Function> _logger;
    protected IMediator _mediator;
    public Function()
    {
        Bootup();
    }
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var sw = Stopwatch.StartNew();
        var body = request.Body ?? string.Empty;
        try
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to process event");
            throw;
        }
        finally
        {
            sw.Stop();
        }
    }

    protected virtual void Bootup()
    {
        try
        {
            var services = Bootstrapper.CreateBootstrapper()
                .BuildConfiguration()
                .Use<Startup>()
                .Run();

            _logger = services.GetRequiredService<ILogger<Function>>();
            _mediator = services.GetRequiredService<IMediator>();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine($"FAILURE: Function Bootup Failure: {ex}");
        }
    }
}