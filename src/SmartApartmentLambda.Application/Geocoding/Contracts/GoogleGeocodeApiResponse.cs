using System.Text.Json.Serialization;

namespace SmartApartmentLambda.Application.Geocoding.Contracts;

public sealed record GoogleGeocodeApiResponse(
    [property: JsonPropertyName("results")] IReadOnlyList<GoogleGeocodeResult>? Results);

public sealed record GoogleGeocodeResult(
    [property: JsonPropertyName("place")] string? Place,
    [property: JsonPropertyName("placeId")] string? PlaceId,
    [property: JsonPropertyName("location")] GoogleGeocodeCoordinates? Location,
    [property: JsonPropertyName("granularity")] string? Granularity,
    [property: JsonPropertyName("viewport")] GoogleGeocodeViewport? Viewport,
    [property: JsonPropertyName("formattedAddress")] string? FormattedAddress,
    [property: JsonPropertyName("postalAddress")] GooglePostalAddress? PostalAddress,
    [property: JsonPropertyName("addressComponents")] IReadOnlyList<GoogleAddressComponent>? AddressComponents,
    [property: JsonPropertyName("types")] IReadOnlyList<string>? Types,
    [property: JsonPropertyName("plusCode")] GooglePlusCode? PlusCode);

public sealed record GoogleGeocodeCoordinates(
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude);

public sealed record GoogleGeocodeViewport(
    [property: JsonPropertyName("low")] GoogleGeocodeCoordinates? Low,
    [property: JsonPropertyName("high")] GoogleGeocodeCoordinates? High);

public sealed record GooglePostalAddress(
    [property: JsonPropertyName("regionCode")] string? RegionCode,
    [property: JsonPropertyName("languageCode")] string? LanguageCode,
    [property: JsonPropertyName("postalCode")] string? PostalCode,
    [property: JsonPropertyName("administrativeArea")] string? AdministrativeArea,
    [property: JsonPropertyName("locality")] string? Locality,
    [property: JsonPropertyName("addressLines")] IReadOnlyList<string>? AddressLines);

public sealed record GoogleAddressComponent(
    [property: JsonPropertyName("longText")] string? LongText,
    [property: JsonPropertyName("shortText")] string? ShortText,
    [property: JsonPropertyName("types")] IReadOnlyList<string>? Types,
    [property: JsonPropertyName("languageCode")] string? LanguageCode);

public sealed record GooglePlusCode(
    [property: JsonPropertyName("globalCode")] string? GlobalCode,
    [property: JsonPropertyName("compoundCode")] string? CompoundCode);