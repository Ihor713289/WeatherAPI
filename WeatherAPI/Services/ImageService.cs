using WeatherAPI.DTOs;
using WeatherAPI.Repositories.Blobs;
using WeatherAPI.Shared.Contracts;
using WeatherAPI.Shared.Queues;
using WeatherAPI.Shared.Tables;

namespace WeatherAPI.Services;

public sealed class ImageService(
    IQueuePublisher<StartGenerationMessage> startPublisher,
    ICreationProcessRepository processes,
    IGeneratedImageRepository images,
    IImageBlobRepository blobs,
    ILogger<ImageService> logger) : IImageService
{
    public async Task<GenerationResponse> StartGenerationAsync(Func<string, string> statusUrl,
        CancellationToken cancellationToken = default)
    {
        var processId = Guid.NewGuid().ToString("N");

        // Persist first, then enqueue: the starter job must always find the row it is asked to process.
        var process = await processes.CreateAsync(processId, cancellationToken);

        try
        {
            await startPublisher.PublishAsync(new StartGenerationMessage(processId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not queue process {ProcessId}", processId);
            await processes.SetStatusAsync(processId, CreationStatus.Failed, "Could not queue the process.",
                CancellationToken.None);
            throw;
        }

        return new GenerationResponse(processId, process.Status, statusUrl(processId), process.CreatedAt);
    }

    public async Task<FullGenerationResponse?> GetGenerationAsync(string id,
        CancellationToken cancellationToken = default)
    {
        var process = await processes.GetAsync(id, cancellationToken);
        if (process is null)
        {
            return null;
        }

        var rows = await images.GetByProcessAsync(id, cancellationToken);

        var imageResponses = rows
            .OrderBy(r => r.StationName)
            .Select(r => new GeneratedImageResponse(
                r.RowKey,
                r.StationName,
                r.Status,
                r.Status == ImageStatus.Completed && r.BlobName is not null
                    ? blobs.GetReadUrl(r.BlobName).ToString()
                    : null,
                r.Error))
            .ToList();

        return new FullGenerationResponse(
            id,
            process.Status,
            process.CreatedAt,
            process.ExpectedImages,
            rows.Count(r => r.Status == ImageStatus.Completed),
            rows.Count(r => r.Status == ImageStatus.Failed),
            process.Error,
            imageResponses);
    }
}
