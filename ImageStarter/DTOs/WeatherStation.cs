using System.Text.Json.Serialization;

namespace ImageStarter.DTOs;

public record WeatherStation(
    [property: JsonPropertyName("stationid")]
    string Id,

    [property: JsonPropertyName("stationname")]
    string Name,

    [property: JsonPropertyName("lat")]
    string Latitude,

    [property: JsonPropertyName("lon")]
    string Longitude,

    [property: JsonPropertyName("regio")]
    string Region,

    [property: JsonPropertyName("iconurl")]
    string IconUrl,

    [property: JsonPropertyName("temperature")]
    float Temperature
    );