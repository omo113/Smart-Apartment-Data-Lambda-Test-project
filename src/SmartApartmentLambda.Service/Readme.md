# SmartApartment Geocode Lambda

This solution implements an AWS Lambda endpoint that accepts a full U.S. address on `GET /Geocode?address=...`, calls the Google Geocoding API, returns the full Google JSON response unchanged, and caches stable responses in DynamoDB for 30 days.

## Architecture

- `SmartApartmentLambda.Service`
  - Lambda runtime entrypoint in `Program.cs`
  - Thin HTTP handler in `Function.cs`
  - Request validation and HTTP response mapping
- `SmartApartmentLambda.Application`
  - MediatR query/handler for geocoding orchestration
  - Address normalization and hashed logging helpers
  - Contracts for cache, Google client, and secrets access
- `SmartApartmentLambda.Infrastructure`
  - DynamoDB cache repository
  - Google Geocoding HTTP client
  - Secrets Manager API key provider
  - Strongly typed options and DI wiring
- `SmartApartmentLambda.Tests`
  - Unit tests for cache behavior and Lambda HTTP mapping

## Request Contract

- Method: `GET`
- Query parameter: `address`
- Example:

```text
https://<api-host>/Geocode?address=70%20Vanderbilt%20Ave,%20New%20York,%20NY%2010017,%20United%20States
```

## Response Behavior

- Success:
  - Returns Google’s raw JSON response body unchanged
  - Sets `Content-Type: application/json`
  - Sets `X-Cache-Status: HIT`, `MISS`, or `REFRESH`
- Errors:
  - `400` when `address` is missing or blank
  - `405` for non-GET requests
  - `502` for Google transport/timeouts or malformed upstream JSON
  - `500` for internal configuration, Secrets Manager, or DynamoDB failures

## Caching Design

- Cache key:
  - Trim leading/trailing whitespace
  - Collapse repeated internal whitespace
  - Lowercase the full address
- Cache store:
  - DynamoDB partition key: `NormalizedAddress` (String)
  - Additional attributes:
    - `OriginalAddress`
    - `ResponseBody`
    - `GoogleStatus`
    - `CreatedAtUtc`
    - `ExpiresAtUtc`
    - `TtlEpochSeconds`
- Cache policy:
  - Cache only the successful geocode states `OK` and `ZERO_RESULTS`
  - Treat non-successful Google API responses as upstream failures instead of cacheable statuses
  - Refresh after 30 days even if DynamoDB TTL has not deleted the record yet
- DynamoDB TTL:
  - Enable TTL on `TtlEpochSeconds`

## Configuration And Secrets

Non-secret settings are stored in `appsettings.json` and can be overridden with environment variables.

The default Google configuration targets Geocoding API v4 address lookups at `https://geocode.googleapis.com/v4/geocode/address/{address}`.

- `GeocodeCache__TableName`
- `GeocodeCache__CacheDurationDays`
- `GoogleGeocoding__BaseUrl`
- `GoogleGeocoding__GeocodePath`
- `GoogleGeocoding__TimeoutSeconds`
- `GoogleGeocoding__MaxRequestsPerSecond`
- `GoogleGeocoding__ApiKeySecretName`
- `GoogleGeocoding__ApiKeySecretJsonKey`

Google API key handling:

- Production:
  - Store the key in AWS Secrets Manager
  - Set `GoogleGeocoding__ApiKeySecretName` on the Lambda
  - If the secret is JSON, the provider reads the `apiKey` property by default
- Local non-production override:
  - Set `GOOGLE_GEOCODING_API_KEY`

The client rate limits outbound Google requests to `25` requests per second by default for each Lambda execution environment. Override this with `GoogleGeocoding__MaxRequestsPerSecond` when needed.

## AWS Setup

Create these resources outside the repo:

1. Lambda function
   - Runtime: `.NET 8`
   - Handler: `SmartApartmentLambda.Service`
2. API Gateway route
   - Route: `GET /Geocode`
   - Integration: this Lambda
3. DynamoDB table
   - Table name: match `GeocodeCache__TableName`
   - Partition key: `NormalizedAddress` (String)
   - TTL attribute: `TtlEpochSeconds`
4. Secrets Manager secret
   - Either raw API key string or JSON with `{ "apiKey": "<value>" }`
5. IAM permissions for the Lambda role
   - `dynamodb:GetItem`
   - `dynamodb:PutItem`
   - `secretsmanager:GetSecretValue`
   - CloudWatch Logs write permissions

## Logging And Observability

- Structured logs include:
  - `AwsRequestId`
  - API Gateway request id
  - cache outcome
  - Google status
  - upstream latency
  - a hash of the normalized address
- Full addresses are not written at info level
- Lambda logs flow to CloudWatch via the standard AWS logging pipeline

## Validation

Build:

```bash
dotnet build SmartApartmentLambda.sln
```

Run tests:

```bash
dotnet test SmartApartmentLambda.Tests/SmartApartmentLambda.Tests.csproj --no-build
```

Use the AWS .NET Mock Lambda Test Tool to invoke the Lambda locally with the handler configured in `aws-lambda-tools-defaults.json`.

## Suggested Demo Flow

1. Show the project structure and explain the three-layer split.
2. Open `Program.cs`, `Function.cs`, the application query handler, and the DynamoDB repository.
3. Show how the Google API key is resolved from Secrets Manager and how local override works.
4. Invoke the endpoint with a sample U.S. address.
5. Show the full Google response body and the `X-Cache-Status: MISS` header on the first call.
6. Invoke the same address again and show `X-Cache-Status: HIT`.
7. Explain that records older than 30 days are treated as expired even before DynamoDB physically removes them.
8. Show the test suite and mention the covered scenarios: cache hit, miss, refresh, zero results, non-cacheable statuses, and HTTP error mapping.
