using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using WeatherAPI.DTOs;

namespace WeatherAPI.Messaging;

public class AzureQueuePublisher : IQueuePublisher
{
    private readonly QueueClient _queueClient;

    public AzureQueuePublisher(IConfiguration configuration)
    {
        _queueClient = new QueueClient(
            configuration["QueueConfig:ConnectionString"],
            configuration["QueueConfig:QueueName"]
        );
        _queueClient.CreateIfNotExistsAsync(cancellationToken: default);
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
    
    public async Task<QueueMessagePeekResult?> PeekAsync(
        string messageId,
        int maxMessages = 32,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }
        
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        maxMessages = 32;
        PeekedMessage[] messages = await _queueClient.PeekMessagesAsync(
            maxMessages: maxMessages,
            cancellationToken: cancellationToken);

        var message = messages.FirstOrDefault(x => x.MessageId == messageId);

        if (message is null)
        {
            return null;
        }

        return new QueueMessagePeekResult(
            message.MessageId,
            message.Body.ToString(),
            message.InsertedOn,
            message.ExpiresOn,
            message.DequeueCount);
    }
}