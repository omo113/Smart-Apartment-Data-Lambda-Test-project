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

LoadLocalEnvironmentVariables();

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

static void LoadLocalEnvironmentVariables()
{
    foreach (var candidatePath in GetEnvFilePaths())
    {
        if (!File.Exists(candidatePath))
        {
            continue;
        }

        foreach (var line in File.ReadLines(candidatePath))
        {
            var trimmedLine = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmedLine.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmedLine[..separatorIndex].Trim();
            if (string.IsNullOrWhiteSpace(key) || Environment.GetEnvironmentVariable(key) is not null)
            {
                continue;
            }

            var value = trimmedLine[(separatorIndex + 1)..].Trim();
            value = TrimWrappingQuotes(value);
            Environment.SetEnvironmentVariable(key, value);
        }

        break;
    }
}

static IEnumerable<string> GetEnvFilePaths()
{
    var currentDirectory = Directory.GetCurrentDirectory();

    yield return Path.Combine(currentDirectory, ".env");

    var parentDirectory = Directory.GetParent(currentDirectory);
    if (parentDirectory is not null)
    {
        yield return Path.Combine(parentDirectory.FullName, ".env");
    }
}

static string TrimWrappingQuotes(string value)
{
    if (value.Length >= 2)
    {
        var startsAndEndsWithDoubleQuotes = value.StartsWith('"') && value.EndsWith('"');
        var startsAndEndsWithSingleQuotes = value.StartsWith('\'') && value.EndsWith('\'');

        if (startsAndEndsWithDoubleQuotes || startsAndEndsWithSingleQuotes)
        {
            return value[1..^1];
        }
    }

    return value;
}
