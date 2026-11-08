using WeatherAPI.Shared.Tables;

namespace WeatherAPI.DTOs;

public sealed record GenerationResponse(string Id, CreationStatus Status, string StatusUrl, DateTimeOffset CreatedAt);

public sealed record FullGenerationResponse(
    string Id,
    CreationStatus Status,
    DateTimeOffset CreatedAt,
    int ExpectedImages,
    int CompletedImages,
    int FailedImages,
    string? Error,
    IReadOnlyList<GeneratedImageResponse> Images);

/// <summary><see cref="Url"/> is a time-limited read-only SAS URL, only set once the image is completed.</summary>
public sealed record GeneratedImageResponse(
    string StationId,
    string StationName,
    ImageStatus Status,
    string? Url,
    string? Error);
