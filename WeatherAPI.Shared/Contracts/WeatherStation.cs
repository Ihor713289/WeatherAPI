namespace WeatherAPI.Shared.Contracts;

/// <summary>
/// Weather data for one station as passed between the jobs. Deliberately independent of the Buienradar JSON shape.
/// </summary>
public sealed record WeatherStation(
    int Id,
    string Name,
    string Region,
    double Latitude,
    double Longitude,
    float? Temperature,
    string? WeatherDescription,
    string? IconUrl);
