namespace WeatherAPI.DTOs;

public sealed record QueueMessagePeekResult(
    string MessageId,
    string Body,
    DateTimeOffset? InsertedOn,
    DateTimeOffset? ExpiresOn,
    long DequeueCount);