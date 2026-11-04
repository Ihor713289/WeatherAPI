using System.Text.Json.Serialization;

namespace ImageStarter.DTOs;

public record BuienradarActual(
    [property: JsonPropertyName("stationmeasurements")]
    List<WeatherStation>? StationMeasurements);