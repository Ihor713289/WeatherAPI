using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageStarter.DTOs;
using ImageStarter.Services;
using WeatherAPI.Messaging;
using System.Text.Json;

namespace ImageStarter;

public class Worker(IConfiguration _configuration,
    IQueuePublisher _queuePublisher,
    IStationService _stationService,
    ILogger _logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            QueueMessage message = await GetNextProcessMessage(stoppingToken);
            if (message != null)
            {
                await ProcessMessage(stoppingToken);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    private Task<QueueMessage> GetNextProcessMessage(CancellationToken cancellationToken)
    {
        return _queuePublisher.ReceiveMessageAsync(cancellationToken);
    }
    
    private IQueuePublisher ConfigureImageQueuePublisher()
    {
        _queuePublisher = _queuePublisher.ChangeQueueName(_configuration, "ImagesQueueName");
        return _queuePublisher;
    }

    private async Task ProcessMessage(CancellationToken cancellationToken)
    {
        var stations = await _stationService.GetStations();
        if (stations == null)
        {
            return;
        }
        ConfigureImageQueuePublisher();
        foreach (var station in stations)
        {
            string message = StationToMessage(station);
            await PublishStationMessage(message, cancellationToken);
        }
    }

    private async Task PublishStationMessage(string message, CancellationToken cancellationToken)
    {
        await _queuePublisher.PublishAsync(message, cancellationToken);
    }
    
    private String StationToMessage(WeatherStation station)
    {
        return JsonSerializer.Serialize(station);
    }
}