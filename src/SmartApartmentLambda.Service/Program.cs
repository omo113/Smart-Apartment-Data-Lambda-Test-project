using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using SmartApartmentLambda.Service;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

using var host = LambdaHostBootstrap.BuildHost(args);
var function = host.Services.GetRequiredService<Function>();

await LambdaBootstrapBuilder
    .Create<APIGatewayProxyRequest, APIGatewayProxyResponse>(
        function.FunctionHandlerAsync,
        new DefaultLambdaJsonSerializer())
    .Build()
    .RunAsync();
