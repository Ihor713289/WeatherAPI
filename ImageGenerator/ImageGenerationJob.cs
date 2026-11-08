using ImageGenerator.Services;
using Microsoft.Extensions.Options;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Contracts;
using WeatherAPI.Shared.Jobs;
using WeatherAPI.Shared.Queues;
using WeatherAPI.Shared.Tables;

namespace ImageGenerator;

/// <summary>
/// Triggered by the images queue. Renders one image per station message, records the result in Table storage
/// and marks the whole process as finished once every station has an outcome.
/// </summary>
public sealed class ImageGenerationJob(
    IQueueReader<StationImageMessage> reader,
    IImageService imageService,
    ICreationProcessRepository processes,
    IGeneratedImageRepository images,
    IOptions<QueueJobOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<ImageGenerationJob> logger)
    : QueueProcessingJob<StationImageMessage>(reader, options, lifetime, logger)
{
    protected override async Task HandleAsync(StationImageMessage message, CancellationToken cancellationToken)
    {
        var (processId, station) = message;

        await images.SetStatusAsync(processId, station.Id, ImageStatus.Processing,
            cancellationToken: cancellationToken);

        var blobName = await imageService.GenerateImageAsync(processId, station, cancellationToken);

        await images.SetStatusAsync(processId, station.Id, ImageStatus.Completed, blobName,
            cancellationToken: cancellationToken);
        await TryCompleteProcessAsync(processId, cancellationToken);
    }

    protected override async Task OnGaveUpAsync(StationImageMessage message, Exception exception,
        CancellationToken cancellationToken)
    {
        await images.SetStatusAsync(message.ProcessId, message.Station.Id, ImageStatus.Failed,
            error: exception.Message, cancellationToken: cancellationToken);
        await TryCompleteProcessAsync(message.ProcessId, cancellationToken);
    }

    /// <summary>
    /// Fan-in: each execution writes its own row first and then checks the totals, so whichever execution
    /// finishes last always sees every outcome. Several executions may set the same final status; that's fine.
    /// </summary>
    private async Task TryCompleteProcessAsync(string processId, CancellationToken cancellationToken)
    {
        var process = await processes.GetAsync(processId, cancellationToken);
        if (process is null || process.ExpectedImages == 0 ||
            process.Status is CreationStatus.Completed or CreationStatus.Failed)
        {
            return;
        }

        var rows = await images.GetByProcessAsync(processId, cancellationToken);
        var completed = rows.Count(r => r.Status == ImageStatus.Completed);
        var failed = rows.Count(r => r.Status == ImageStatus.Failed);

        if (completed + failed < process.ExpectedImages)
        {
            return;
        }

        var status = completed > 0 ? CreationStatus.Completed : CreationStatus.Failed;
        var error = failed > 0 ? $"{failed} of {process.ExpectedImages} images failed." : null;
        await processes.SetStatusAsync(processId, status, error, cancellationToken);

        logger.LogInformation("Process {ProcessId} finished as {Status} ({Completed} ok, {Failed} failed)",
            processId, status, completed, failed);
    }
}
