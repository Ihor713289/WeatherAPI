using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace WeatherAPI.Shared.Queues;

/// <summary>Publishes messages of type <typeparamref name="T"/> to the queue registered for that type.</summary>
public interface IQueuePublisher<in T>
{
    Task<string> PublishAsync(T message, CancellationToken cancellationToken = default);
}

/// <summary>A dequeued message. <see cref="Body"/> is null when the payload could not be deserialized.</summary>
public sealed record ReceivedMessage<T>(T? Body, string RawBody, string MessageId, string PopReceipt, long DequeueCount);

/// <summary>Reads and settles messages of type <typeparamref name="T"/> from the queue registered for that type.</summary>
public interface IQueueReader<T>
{
    Task<IReadOnlyList<ReceivedMessage<T>>> ReceiveAsync(int maxMessages, TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(ReceivedMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>Makes the message visible again after <paramref name="delay"/> so it is retried.</summary>
    Task RetryLaterAsync(ReceivedMessage<T> message, TimeSpan delay, CancellationToken cancellationToken = default);

    /// <summary>Copies the message to "&lt;queue&gt;-poison" and removes it from the work queue.</summary>
    Task MoveToPoisonAsync(ReceivedMessage<T> message, CancellationToken cancellationToken = default);
}

internal static class QueueSerializer
{
    // Reject payloads with missing/null required values instead of silently producing half-empty records.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true
    };
}

internal sealed class AzureQueuePublisher<T>(QueueClient queue) : IQueuePublisher<T>
{
    public async Task<string> PublishAsync(T message, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(message, QueueSerializer.Options);
        var receipt = await queue.SendMessageAsync(body, cancellationToken);
        return receipt.Value.MessageId;
    }
}

internal sealed class AzureQueueReader<T>(QueueClient queue, QueueClient poisonQueue) : IQueueReader<T>
{
    // Storage Queues return at most 32 messages per receive call.
    private const int MaxBatchSize = 32;

    public async Task<IReadOnlyList<ReceivedMessage<T>>> ReceiveAsync(int maxMessages, TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default)
    {
        QueueMessage[] messages = await queue.ReceiveMessagesAsync(
            Math.Clamp(maxMessages, 1, MaxBatchSize), visibilityTimeout, cancellationToken);

        return messages
            .Select(m => new ReceivedMessage<T>(TryDeserialize(m.MessageText), m.MessageText, m.MessageId,
                m.PopReceipt, m.DequeueCount))
            .ToList();
    }

    public Task DeleteAsync(ReceivedMessage<T> message, CancellationToken cancellationToken = default) =>
        queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);

    public Task RetryLaterAsync(ReceivedMessage<T> message, TimeSpan delay,
        CancellationToken cancellationToken = default) =>
        queue.UpdateMessageAsync(message.MessageId, message.PopReceipt, visibilityTimeout: delay,
            cancellationToken: cancellationToken);

    public async Task MoveToPoisonAsync(ReceivedMessage<T> message, CancellationToken cancellationToken = default)
    {
        await poisonQueue.SendMessageAsync(message.RawBody, cancellationToken);
        await DeleteAsync(message, cancellationToken);
    }

    private static T? TryDeserialize(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, QueueSerializer.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
