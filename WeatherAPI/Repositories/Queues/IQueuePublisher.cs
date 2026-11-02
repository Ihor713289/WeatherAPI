using Azure;
using Azure.Storage.Queues.Models;
using WeatherAPI.DTOs;

namespace WeatherAPI.Messaging;

public interface IQueuePublisher
{
    Task<string> PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);
    
    Task<Response> DeleteAsync(
        string messageId,
        string popReceipt,
        CancellationToken cancellationToken = default);
    
    Task<QueueMessage> ReceiveMessageAsync(
        CancellationToken cancellationToken = default);
    
    IQueuePublisher ChangeQueueName(IConfiguration configuration, string queueName);
}