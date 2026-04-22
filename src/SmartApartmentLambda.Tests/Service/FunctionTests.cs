using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Service;
using Xunit;

namespace SmartApartmentLambda.Tests.Service;

public sealed class FunctionTests
{
    [Fact]
    public async Task FunctionHandlerAsync_ReturnsBadRequest_WhenAddressIsMissing()
    {
        var function = CreateFunction((_, _) => Task.FromResult<object?>(new GetGeocodeResult("{}", GeocodeCacheStatus.Miss, "OK")));

        var response = await function.FunctionHandlerAsync(
            new APIGatewayProxyRequest
            {
                HttpMethod = "GET",
                QueryStringParameters = new Dictionary<string, string>()
            },
            new TestLambdaContext());

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        Assert.Contains("address", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(GeocodeCacheStatus.Hit, "HIT")]
    [InlineData(GeocodeCacheStatus.Miss, "MISS")]
    [InlineData(GeocodeCacheStatus.Refresh, "REFRESH")]
    public async Task FunctionHandlerAsync_ReturnsRawGoogleBodyAndCacheHeader(GeocodeCacheStatus cacheStatus, string expectedHeaderValue)
    {
        const string googleBody = "{\"status\":\"OK\",\"results\":[{\"place_id\":\"abc123\"}]}";
        var function = CreateFunction((_, _) => Task.FromResult<object?>(new GetGeocodeResult(googleBody, cacheStatus, "OK")));

        var response = await function.FunctionHandlerAsync(
            CreateGetRequest(),
            new TestLambdaContext());

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(googleBody, response.Body);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        Assert.Equal(expectedHeaderValue, response.Headers["X-Cache-Status"]);
    }

    [Fact]
    public async Task FunctionHandlerAsync_ReturnsBadGateway_WhenMediatorThrowsUpstreamException()
    {
        var function = CreateFunction((_, _) => Task.FromException<object?>(new UpstreamServiceException("timeout")));

        var response = await function.FunctionHandlerAsync(
            CreateGetRequest(),
            new TestLambdaContext());

        Assert.Equal(502, response.StatusCode);
        Assert.Contains("Google", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FunctionHandlerAsync_ReturnsInternalServerError_WhenMediatorThrowsInternalException()
    {
        var function = CreateFunction((_, _) => Task.FromException<object?>(new InternalServiceException("dynamodb failed")));

        var response = await function.FunctionHandlerAsync(
            CreateGetRequest(),
            new TestLambdaContext());

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("internal", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static Function CreateFunction(Func<object, CancellationToken, Task<object?>> sendHandler)
    {
        return new Function(new TestMediator(sendHandler), NullLogger<Function>.Instance);
    }

    private static APIGatewayProxyRequest CreateGetRequest()
    {
        return new APIGatewayProxyRequest
        {
            HttpMethod = "GET",
            QueryStringParameters = new Dictionary<string, string>
            {
                ["address"] = "70 Vanderbilt Ave, New York, NY 10017, United States"
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                RequestId = "gateway-request-id"
            }
        };
    }

    private sealed class TestMediator : IMediator
    {
        private readonly Func<object, CancellationToken, Task<object?>> _sendHandler;

        public TestMediator(Func<object, CancellationToken, Task<object?>> sendHandler)
        {
            _sendHandler = sendHandler;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var result = await _sendHandler(request, cancellationToken);
            return (TResponse)result!;
        }

        public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            _ = await _sendHandler(request!, cancellationToken);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return _sendHandler(request, cancellationToken);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestLambdaContext : ILambdaContext
    {
        public string AwsRequestId => "aws-request-id";
        public IClientContext ClientContext => throw new NotSupportedException();
        public string FunctionName => "SmartApartmentLambda";
        public string FunctionVersion => "1";
        public ICognitoIdentity Identity => throw new NotSupportedException();
        public string InvokedFunctionArn => "arn:aws:lambda:region:account:function:SmartApartmentLambda";
        public ILambdaLogger Logger { get; } = new TestLambdaLogger();
        public string LogGroupName => "/aws/lambda/SmartApartmentLambda";
        public string LogStreamName => "2026/04/22/[1]abcdef";
        public int MemoryLimitInMB => 512;
        public TimeSpan RemainingTime => TimeSpan.FromMinutes(1);
    }

    private sealed class TestLambdaLogger : ILambdaLogger
    {
        public void Log(string message)
        {
        }

        public void LogLine(string message)
        {
        }
    }
}
