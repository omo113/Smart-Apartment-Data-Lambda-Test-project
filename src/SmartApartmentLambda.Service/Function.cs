using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Application.Queries;

namespace SmartApartmentLambda.Service;

public sealed class Function
{
    private static readonly JsonSerializerOptions ErrorJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<Function> _logger;
    private readonly IMediator _mediator;

    public Function(IMediator mediator, ILogger<Function> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandlerAsync(APIGatewayProxyRequest request, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["AwsRequestId"] = context.AwsRequestId,
            ["ApiGatewayRequestId"] = request.RequestContext?.RequestId
        });

        if (!string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrorResponse(405, "Only GET requests are supported.");
        }

        if (request.QueryStringParameters is null ||
            !request.QueryStringParameters.TryGetValue("address", out var address) ||
            string.IsNullOrWhiteSpace(address))
        {
            return CreateErrorResponse(400, "The 'address' query parameter is required.");
        }

        try
        {
            var result = await _mediator.Send(new GetGeocodeQuery(address), CancellationToken.None);

            _logger.LogInformation(
                "Processed geocode request with cache status {CacheStatus} and Google status {GoogleStatus}",
                result.CacheStatus,
                result.GoogleStatus);

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = result.ResponseBody,
                Headers = CreateJsonHeaders(result.CacheStatus)
            };
        }
        catch (UpstreamServiceException ex)
        {
            _logger.LogError(ex, "Google geocoding request failed");
            return CreateErrorResponse(502, "Failed to retrieve geocode response from Google.");
        }
        catch (InternalServiceException ex)
        {
            _logger.LogError(ex, "Internal geocode processing failed");
            return CreateErrorResponse(500, "An internal error occurred while processing the geocode request.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled geocode processing failure");
            return CreateErrorResponse(500, "An internal error occurred while processing the geocode request.");
        }
    }

    private static APIGatewayProxyResponse CreateErrorResponse(int statusCode, string message)
    {
        return new APIGatewayProxyResponse
        {
            StatusCode = statusCode,
            Body = JsonSerializer.Serialize(new { message }, ErrorJsonOptions),
            Headers = CreateJsonHeaders()
        };
    }

    private static Dictionary<string, string> CreateJsonHeaders(GeocodeCacheStatus? cacheStatus = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = "application/json"
        };

        if (cacheStatus is not null)
        {
            headers["X-Cache-Status"] = cacheStatus.Value.ToString().ToUpperInvariant();
        }

        return headers;
    }
}
