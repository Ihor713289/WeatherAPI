using Azure;
using WeatherAPI.Enums;

namespace WeatherAPI.DTOs;

public record GeneratedImageResponse(
    ImageStatus Status,
    string SasUrl,
    ETag ETag,
    string StationName);