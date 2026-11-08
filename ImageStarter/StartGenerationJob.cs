using ImageStarter.Services;
using Microsoft.Extensions.Options;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Contracts;
using WeatherAPI.Shared.Jobs;
using WeatherAPI.Shared.Queues;
using WeatherAPI.Shared.Tables;

namespace ImageStarter;

/// <summary>
/// Triggered by the processes queue. Fetches the stations once and fans out one message per station
/// onto the images queue. Every step is idempotent so a redelivered start message is harmless.
/// </summary>
public sealed class StartGenerationJob(
    IQueueReader<StartGenerationMessage> reader,
    IQueuePublisher<StationImageMessage> imagePublisher,
    IStationService stationService,
    ICreationProcessRepository processes,
    IGeneratedImageRepository images,
    IOptions<QueueJobOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger<StartGenerationJob> logger)
    : QueueProcessingJob<StartGenerationMessage>(reader, options, lifetime, logger)
{
    protected override async Task HandleAsync(StartGenerationMessage message, CancellationToken cancellationToken)
    {
        var processId = message.ProcessId;
        var process = await processes.GetAsync(processId, cancellationToken);

        if (process is null)
        {
            logger.LogWarning("Process {ProcessId} does not exist, ignoring start message", processId);
            return;
        }

        if (process.Status is CreationStatus.Completed or CreationStatus.Failed)
        {
            logger.LogInformation("Process {ProcessId} is already {Status}, ignoring start message", processId,
                process.Status);
            return;
        }

        var stations = await stationService.GetStationsAsync(cancellationToken);
        if (stations.Count == 0)
        {
            throw new InvalidOperationException("Buienradar returned no weather stations.");
        }

        // Order matters: rows and the expected count must exist before any generator can finish an image.
        await images.AddPendingAsync(processId, stations, cancellationToken);
        await processes.MarkProcessingAsync(processId, stations.Count, cancellationToken);

        await Parallel.ForEachAsync(stations, cancellationToken, async (station, ct) =>
            await imagePublisher.PublishAsync(new StationImageMessage(processId, station), ct));

        logger.LogInformation("Process {ProcessId}: queued {Count} station images", processId, stations.Count);
    }

    protected override Task OnGaveUpAsync(StartGenerationMessage message, Exception exception,
        CancellationToken cancellationToken) =>
        processes.SetStatusAsync(message.ProcessId, CreationStatus.Failed,
            $"Could not start image generation: {exception.Message}", cancellationToken);
}
