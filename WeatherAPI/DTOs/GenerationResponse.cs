using Azure;
using WeatherAPI.Enums;

namespace WeatherAPI.DTOs;

public record GenerationResponse(string id, CreationStatus creationStatus, ETag etag, DateTimeOffset? timestamp);