using System.Text.Json;
using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using WeatherAPI.DTOs;

namespace WeatherAPI.Messaging;

public class AzureQueuePublisher : IQueuePublisher
{
    private QueueClient _queueClient;

    public AzureQueuePublisher(IConfiguration configuration)
    {
        _queueClient = new QueueClient(
            configuration["QueueConfig:ConnectionString"],
            configuration["QueueConfig:ProcessesQueueName"]
        );
        _queueClient.CreateIfNotExistsAsync(cancellationToken: default);
    }
    
    
    public IQueuePublisher ChangeQueueName(IConfiguration configuration, string queueName)
    {
        _queueClient = new QueueClient(
            configuration["QueueConfig:ConnectionString"],
            configuration[$"QueueConfig:{queueName}"]
        );
        _queueClient.CreateIfNotExistsAsync(cancellationToken: default);
        return this;
    }

    public async Task<string> PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var body = JsonSerializer.Serialize(message);

        SendReceipt receipt = await _queueClient.SendMessageAsync(
            body,
            cancellationToken: cancellationToken);

        return receipt.MessageId;
    }

    public async Task<QueueMessage> ReceiveMessageAsync(
        CancellationToken cancellationToken = default)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        QueueMessage message = await _queueClient.ReceiveMessageAsync(
            cancellationToken: cancellationToken);

        return message;
    }

    public async Task<Response> DeleteAsync(
        string messageId, string popReceipt, CancellationToken cancellationToken
    )
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        return await _queueClient.DeleteMessageAsync(
            messageId, popReceipt, cancellationToken);
    }
}