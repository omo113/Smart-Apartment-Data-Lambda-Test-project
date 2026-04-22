using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartApartmentLambda.Application.Geocoding;
using SmartApartmentLambda.Infrastructure.Configuration;

namespace SmartApartmentLambda.Infrastructure.Geocoding;

public sealed class DynamoDbGeocodeCacheRepository : IGeocodeCacheRepository
{
    private const string NormalizedAddressAttributeName = "NormalizedAddress";
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ILogger<DynamoDbGeocodeCacheRepository> _logger;
    private readonly GeocodeCacheOptions _options;

    public DynamoDbGeocodeCacheRepository(
        IAmazonDynamoDB dynamoDb,
        IOptions<GeocodeCacheOptions> options,
        ILogger<DynamoDbGeocodeCacheRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<GeocodeCacheEntry?> GetAsync(string normalizedAddress, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _dynamoDb.GetItemAsync(
                new GetItemRequest
                {
                    TableName = _options.TableName,
                    ConsistentRead = true,
                    Key = new Dictionary<string, AttributeValue>
                    {
                        [NormalizedAddressAttributeName] = new AttributeValue { S = normalizedAddress }
                    }
                },
                cancellationToken);

            if (response.Item is null || response.Item.Count == 0)
            {
                return null;
            }

            return Map(response.Item);
        }
        catch (InternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load geocode cache record from DynamoDB");
            throw new InternalServiceException("Failed to load the geocode cache record.", ex);
        }
    }

    public async Task SaveAsync(GeocodeCacheEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await _dynamoDb.PutItemAsync(
                new PutItemRequest
                {
                    TableName = _options.TableName,
                    Item = new Dictionary<string, AttributeValue>
                    {
                        [NormalizedAddressAttributeName] = new AttributeValue { S = entry.NormalizedAddress },
                        ["OriginalAddress"] = new AttributeValue { S = entry.OriginalAddress },
                        ["ResponseBody"] = new AttributeValue { S = entry.ResponseBody },
                        ["GoogleStatus"] = new AttributeValue { S = entry.GoogleStatus },
                        ["CreatedAtUtc"] = new AttributeValue { S = entry.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture) },
                        ["ExpiresAtUtc"] = new AttributeValue { S = entry.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture) },
                        ["TtlEpochSeconds"] = new AttributeValue { N = entry.TtlEpochSeconds.ToString(CultureInfo.InvariantCulture) }
                    }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save geocode cache record to DynamoDB");
            throw new InternalServiceException("Failed to save the geocode cache record.", ex);
        }
    }

    private static GeocodeCacheEntry Map(IReadOnlyDictionary<string, AttributeValue> item)
    {
        try
        {
            var normalizedAddress = item[NormalizedAddressAttributeName].S;
            var originalAddress = item["OriginalAddress"].S;
            var responseBody = item["ResponseBody"].S;
            var googleStatus = item["GoogleStatus"].S;
            var createdAtUtc = DateTimeOffset.Parse(item["CreatedAtUtc"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expiresAtUtc = DateTimeOffset.Parse(item["ExpiresAtUtc"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var ttlEpochSeconds = long.Parse(item["TtlEpochSeconds"].N, CultureInfo.InvariantCulture);

            return new GeocodeCacheEntry(
                normalizedAddress,
                originalAddress,
                responseBody,
                googleStatus,
                createdAtUtc,
                expiresAtUtc,
                ttlEpochSeconds);
        }
        catch (Exception ex)
        {
            throw new InternalServiceException("The geocode cache record is malformed.", ex);
        }
    }
}
