using System.Text.Json.Serialization;

namespace ImageStarter.DTOs;

// Shape of https://data.buienradar.nl/2.0/feed/json (only the fields we use).

public sealed record BuienradarResponse(
    [property: JsonPropertyName("actual")] BuienradarActual? Actual);

public sealed record BuienradarActual(
    [property: JsonPropertyName("stationmeasurements")] List<BuienradarStation>? StationMeasurements);

public sealed record BuienradarStation(
    [property: JsonPropertyName("stationid")] int StationId,
    [property: JsonPropertyName("stationname")] string? StationName,
    [property: JsonPropertyName("lat")] double Latitude,
    [property: JsonPropertyName("lon")] double Longitude,
    [property: JsonPropertyName("regio")] string? Region,
    [property: JsonPropertyName("weatherdescription")] string? WeatherDescription,
    [property: JsonPropertyName("iconurl")] string? IconUrl,
    [property: JsonPropertyName("fullIconUrl")] string? FullIconUrl,
    // Not every station measures temperature, so this must be nullable.
    [property: JsonPropertyName("temperature")] float? Temperature);
