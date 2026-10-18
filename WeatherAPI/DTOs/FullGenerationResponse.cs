using Azure;
using WeatherAPI.Enums;

namespace WeatherAPI.DTOs;

public record FullGenerationResponse(
    CreationStatus Status,
    ETag ETag,
    DateTimeOffset? Timestamp,
    List<GeneratedImageResponse> GeneratedImages);