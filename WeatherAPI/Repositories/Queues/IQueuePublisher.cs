using WeatherAPI.DTOs;

namespace WeatherAPI.Messaging;

public interface IQueuePublisher
{
    Task<string> PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);
    
    Task<QueueMessagePeekResult?> PeekAsync(
        string messageId,
        int maxMessages = 32,
        CancellationToken cancellationToken = default);
}