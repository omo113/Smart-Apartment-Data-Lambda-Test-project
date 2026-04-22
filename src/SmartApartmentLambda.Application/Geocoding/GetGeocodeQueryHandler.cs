using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace SmartApartmentLambda.Application.Geocoding;

public sealed class GetGeocodeQueryHandler : IRequestHandler<GetGeocodeQuery, GetGeocodeResult>
{
    private static readonly HashSet<string> CacheableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OK",
        "ZERO_RESULTS"
    };

    private readonly IGeocodeCacheRepository _cacheRepository;
    private readonly IGeocodeCachePolicy _cachePolicy;
    private readonly IGoogleGeocodingClient _googleGeocodingClient;
    private readonly ILogger<GetGeocodeQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public GetGeocodeQueryHandler(
        IGeocodeCacheRepository cacheRepository,
        IGeocodeCachePolicy cachePolicy,
        IGoogleGeocodingClient googleGeocodingClient,
        ILogger<GetGeocodeQueryHandler> logger,
        TimeProvider timeProvider)
    {
        _cacheRepository = cacheRepository;
        _cachePolicy = cachePolicy;
        _googleGeocodingClient = googleGeocodingClient;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<GetGeocodeResult> Handle(GetGeocodeQuery request, CancellationToken cancellationToken)
    {
        var normalizedAddress = AddressNormalizer.Normalize(request.Address);
        var addressHash = AddressHasher.ComputeHash(normalizedAddress);
        var now = _timeProvider.GetUtcNow();

        GeocodeCacheEntry? cachedEntry;

        try
        {
            cachedEntry = await _cacheRepository.GetAsync(normalizedAddress, cancellationToken);
        }
        catch (InternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InternalServiceException("Failed to read the geocode cache.", ex);
        }

        if (cachedEntry is not null && cachedEntry.ExpiresAtUtc > now)
        {
            _logger.LogInformation(
                "Handled geocode request for {AddressHash} with cache status {CacheStatus} and Google status {GoogleStatus}",
                addressHash,
                GeocodeCacheStatus.Hit,
                cachedEntry.GoogleStatus);

            return new GetGeocodeResult(cachedEntry.ResponseBody, GeocodeCacheStatus.Hit, cachedEntry.GoogleStatus);
        }

        var cacheStatus = cachedEntry is null ? GeocodeCacheStatus.Miss : GeocodeCacheStatus.Refresh;
        var googleStopwatch = Stopwatch.StartNew();
        var googleResponse = await _googleGeocodingClient.GeocodeAsync(request.Address, cancellationToken);
        googleStopwatch.Stop();

        if (CacheableStatuses.Contains(googleResponse.Status))
        {
            var expiresAtUtc = now.Add(_cachePolicy.CacheDuration);
            var entry = new GeocodeCacheEntry(
                normalizedAddress,
                request.Address,
                googleResponse.ResponseBody,
                googleResponse.Status,
                now,
                expiresAtUtc,
                expiresAtUtc.ToUnixTimeSeconds());

            try
            {
                await _cacheRepository.SaveAsync(entry, cancellationToken);
            }
            catch (InternalServiceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InternalServiceException("Failed to write the geocode cache.", ex);
            }
        }

        _logger.LogInformation(
            "Handled geocode request for {AddressHash} with cache status {CacheStatus}, Google status {GoogleStatus}, and upstream latency {UpstreamLatencyMs}ms",
            addressHash,
            cacheStatus,
            googleResponse.Status,
            googleStopwatch.ElapsedMilliseconds);

        return new GetGeocodeResult(googleResponse.ResponseBody, cacheStatus, googleResponse.Status);
    }
}
