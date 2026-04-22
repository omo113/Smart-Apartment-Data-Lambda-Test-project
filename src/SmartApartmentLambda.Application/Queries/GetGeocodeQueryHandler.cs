using MediatR;
using Microsoft.Extensions.Logging;
using SmartApartmentLambda.Application.Geocoding.Abstractions;
using SmartApartmentLambda.Application.Geocoding.Caching;
using SmartApartmentLambda.Application.Geocoding.Contracts;
using SmartApartmentLambda.Application.Geocoding.Errors;
using SmartApartmentLambda.Application.Geocoding.Normalization;
using System.Diagnostics;
using AddressNormalizer = SmartApartmentLambda.Application.Geocoding.Normalization.AddressNormalizer;

namespace SmartApartmentLambda.Application.Queries;

public sealed class GetGeocodeQueryHandler(
    IGeocodeCacheRepository cacheRepository,
    IGeocodeCachePolicy cachePolicy,
    IGoogleGeocodingClient googleGeocodingClient,
    ILogger<GetGeocodeQueryHandler> logger,
    TimeProvider timeProvider)
    : IRequestHandler<GetGeocodeQuery, GetGeocodeResult>
{
    private static readonly HashSet<string> CacheableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OK",
        "ZERO_RESULTS"
    };

    public async Task<GetGeocodeResult> Handle(GetGeocodeQuery request, CancellationToken cancellationToken)
    {
        var normalizedAddress = AddressNormalizer.Normalize(request.Address);
        var addressHash = AddressHasher.ComputeHash(normalizedAddress);
        var now = timeProvider.GetUtcNow();

        GeocodeCacheEntry? cachedEntry;

        try
        {
            cachedEntry = await cacheRepository.GetAsync(normalizedAddress, cancellationToken);
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
            logger.LogInformation(
                "Handled geocode request for {AddressHash} with cache status {CacheStatus} and Google status {GoogleStatus}",
                addressHash,
                GeocodeCacheStatus.Hit,
                cachedEntry.GoogleStatus);

            return new GetGeocodeResult(cachedEntry.ResponseBody, GeocodeCacheStatus.Hit, cachedEntry.GoogleStatus);
        }

        var cacheStatus = cachedEntry is null ? GeocodeCacheStatus.Miss : GeocodeCacheStatus.Refresh;
        var googleStopwatch = Stopwatch.StartNew();
        var googleResponse = await googleGeocodingClient.GeocodeAsync(request.Address, cancellationToken);
        googleStopwatch.Stop();

        if (CacheableStatuses.Contains(googleResponse.Status))
        {
            var expiresAtUtc = now.Add(cachePolicy.CacheDuration);
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
                await cacheRepository.SaveAsync(entry, cancellationToken);
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

        logger.LogInformation(
            "Handled geocode request for {AddressHash} with cache status {CacheStatus}, Google status {GoogleStatus}, and upstream latency {UpstreamLatencyMs}ms",
            addressHash,
            cacheStatus,
            googleResponse.Status,
            googleStopwatch.ElapsedMilliseconds);

        return new GetGeocodeResult(googleResponse.ResponseBody, cacheStatus, googleResponse.Status);
    }
}
