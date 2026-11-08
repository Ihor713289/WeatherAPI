using Azure;
using Azure.Data.Tables;

namespace WeatherAPI.Shared.Tables;

public enum CreationStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

public enum ImageStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
}

/// <summary>One row per generation process. PartitionKey = RowKey = process id.</summary>
public sealed class CreationProcess : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public CreationStatus Status { get; set; } = CreationStatus.Pending;

    /// <summary>Number of station images the starter job fanned out; 0 until the stations are known.</summary>
    public int ExpectedImages { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? Error { get; set; }
}

/// <summary>One row per station image. PartitionKey = process id, RowKey = station id.</summary>
public sealed class GeneratedImage : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string StationName { get; set; } = string.Empty;

    public ImageStatus Status { get; set; } = ImageStatus.Pending;

    /// <summary>Blob name inside the images container (not a URL; the API turns it into a SAS URL).</summary>
    public string? BlobName { get; set; }

    public string? Error { get; set; }
}
