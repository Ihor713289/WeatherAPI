using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Queues;

namespace WeatherAPI.Shared.Jobs;

/// <summary>
/// Base class for an event-driven Azure Container Apps Job: one execution drains (part of) a queue and then exits.
/// Successful messages are deleted, failures are retried with a delay and moved to the poison queue after
/// <see cref="QueueJobOptions.MaxDequeueCount"/> attempts. A non-zero exit code marks the execution as failed.
/// </summary>
public abstract class QueueProcessingJob<TMessage>(
    IQueueReader<TMessage> reader,
    IOptions<QueueJobOptions> options,
    IHostApplicationLifetime lifetime,
    ILogger logger) : BackgroundService
{
    private readonly QueueJobOptions _options = options.Value;

    /// <summary>Processes one message. Throwing means the message will be retried.</summary>
    protected abstract Task HandleAsync(TMessage message, CancellationToken cancellationToken);

    /// <summary>Called once when a message is given up on (moved to the poison queue).</summary>
    protected virtual Task OnGaveUpAsync(TMessage message, Exception exception, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processed = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested && processed < _options.MaxMessagesPerExecution)
            {
                var messages = await reader.ReceiveAsync(_options.MaxMessagesPerExecution - processed,
                    _options.VisibilityTimeout, stoppingToken);

                if (messages.Count == 0)
                {
                    break;
                }

                await Parallel.ForEachAsync(messages,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrency),
                        CancellationToken = stoppingToken
                    },
                    async (message, ct) =>
                    {
                        try
                        {
                            await ProcessAsync(message, ct);
                        }
                        catch (Exception ex) when (!ct.IsCancellationRequested)
                        {
                            // Settling the message failed (e.g. pop receipt expired). It will simply be
                            // delivered again after its visibility timeout, so don't abort the whole batch.
                            logger.LogError(ex, "Could not settle message {MessageId}", message.MessageId);
                        }
                    });

                processed += messages.Count;
            }

            logger.LogInformation("Job execution finished after {Count} message(s)", processed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Unfinished messages become visible again once their visibility timeout expires.
            logger.LogWarning("Job execution was cancelled after {Count} message(s)", processed);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Job execution failed after {Count} message(s)", processed);
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task ProcessAsync(ReceivedMessage<TMessage> message, CancellationToken cancellationToken)
    {
        if (message.Body is null)
        {
            logger.LogError("Message {MessageId} has an invalid payload, moving it to the poison queue: {Body}",
                message.MessageId, message.RawBody);
            await reader.MoveToPoisonAsync(message, cancellationToken);
            return;
        }

        try
        {
            await HandleAsync(message.Body, cancellationToken);
            await reader.DeleteAsync(message, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            if (message.DequeueCount < _options.MaxDequeueCount)
            {
                logger.LogWarning(ex, "Message {MessageId} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}",
                    message.MessageId, message.DequeueCount, _options.MaxDequeueCount, _options.RetryDelay);
                await reader.RetryLaterAsync(message, _options.RetryDelay, cancellationToken);
                return;
            }

            logger.LogError(ex, "Message {MessageId} failed {Attempts} times, moving it to the poison queue",
                message.MessageId, message.DequeueCount);

            try
            {
                await OnGaveUpAsync(message.Body, ex, cancellationToken);
            }
            catch (Exception callbackException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(callbackException, "Failure handler for message {MessageId} failed", message.MessageId);
            }

            await reader.MoveToPoisonAsync(message, cancellationToken);
        }
    }
}
