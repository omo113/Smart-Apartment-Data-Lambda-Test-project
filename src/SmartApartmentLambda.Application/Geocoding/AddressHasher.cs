using System.Security.Cryptography;
using System.Text;

namespace SmartApartmentLambda.Application.Geocoding;

internal static class AddressHasher
{
    public static string ComputeHash(string normalizedAddress)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedAddress));
        return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
    }
}
