using System.ComponentModel.DataAnnotations;

namespace SmartApartmentLambda.Infrastructure.Configuration;

public sealed class GeocodeCacheOptions
{
    public const string SectionName = "GeocodeCache";

    [Required]
    public string TableName { get; init; } = string.Empty;

    [Range(1, 365)]
    public int CacheDurationDays { get; init; } = 30;
}
