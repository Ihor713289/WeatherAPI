using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ImageStarter.DTOs;
using ImageStarter.Services;
using WeatherAPI.Messaging;
using System.Text.Json;

namespace ImageStarter;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private IConfiguration _configuration;
    private IQueuePublisher _queuePublisher;
    private IStationService _stationService;

    public Worker(IConfiguration configuration, ILogger<Worker> logger, IQueuePublisher queuePublisher, IStationService stationService) : this(logger)
    {
        _configuration = configuration;
        _queuePublisher = queuePublisher;
        _stationService = stationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            QueueMessage message = await GetNextProcessMessage(stoppingToken);
            if (message != null)
            {
                await ProcessMessage(message, stoppingToken);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    protected Task<QueueMessage> GetNextProcessMessage(CancellationToken cancellationToken)
    {
        return _queuePublisher.ReceiveMessageAsync(cancellationToken);
    }


    private IQueuePublisher ConfigureImageQueuePublisher()
    {
        _queuePublisher = _queuePublisher.ChangeQueueName(_configuration, "ImagesQueueName");
        return _queuePublisher;
    }

    private async Task ProcessMessage(QueueMessage message, CancellationToken cancellationToken)
    {
        var stations = await _stationService.GetStations();
        if (stations == null)
        {
            return;
        }
        ConfigureImageQueuePublisher();
        foreach (var station in stations)
        {
            await PublishStationMessage(station, message, cancellationToken);
        }
    }

    private async Task PublishStationMessage(WeatherStation station, QueueMessage message, CancellationToken cancellationToken)
    {
        var stationJson = JsonSerializer.Serialize(station);
        await _queuePublisher.PublishAsync(stationJson, cancellationToken);
    }
}