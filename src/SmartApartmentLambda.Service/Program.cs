using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartApartmentLambda.Application;
using SmartApartmentLambda.Infrastructure;
using SmartApartmentLambda.Service;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<Function>();

var host = builder.Build();
var function = host.Services.GetRequiredService<Function>();

await LambdaBootstrapBuilder
    .Create<APIGatewayProxyRequest, APIGatewayProxyResponse>(
        function.FunctionHandlerAsync,
        new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();
