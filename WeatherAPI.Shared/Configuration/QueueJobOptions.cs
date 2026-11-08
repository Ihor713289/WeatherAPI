namespace WeatherAPI.Shared.Configuration;

/// <summary>
/// Controls how a single Container Apps Job execution drains its queue (bound from the "Job" section).
/// </summary>
public sealed class QueueJobOptions
{
    public const string SectionName = "Job";

    /// <summary>
    /// Stop the execution after this many messages, even if the queue is not empty yet. Defaults to 1:
    /// KEDA starts one job execution per queue message, so the fan-out happens at the job level.
    /// </summary>
    public int MaxMessagesPerExecution { get; set; } = 1;

    /// <summary>Number of messages processed in parallel within one execution.</summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>After this many delivery attempts a message is moved to the poison queue.</summary>
    public int MaxDequeueCount { get; set; } = 5;

    /// <summary>How long a received message stays invisible to other executions.</summary>
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Delay before a failed message becomes visible again for a retry.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}
