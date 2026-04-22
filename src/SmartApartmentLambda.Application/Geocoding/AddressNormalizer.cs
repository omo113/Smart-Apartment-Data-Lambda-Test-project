using System.Text.RegularExpressions;

namespace SmartApartmentLambda.Application.Geocoding;

internal static partial class AddressNormalizer
{
    public static string Normalize(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return CollapseWhitespace().Replace(address.Trim(), " ").ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex CollapseWhitespace();
}
