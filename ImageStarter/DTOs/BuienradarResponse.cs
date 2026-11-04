using System.Text.Json.Serialization;

namespace ImageStarter.DTOs;

public record BuienradarResponse(
    [property: JsonPropertyName("actual")]
    BuienradarActual? Actual);