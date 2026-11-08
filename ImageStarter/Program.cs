using ImageStarter;
using ImageStarter.Services;
using WeatherAPI.Shared;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Contracts;

// Runs as an event-driven Azure Container Apps Job: processes the start queue, then exits.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWeatherStorage(builder.Configuration);
builder.Services.AddQueueReader<StartGenerationMessage>(o => o.ProcessesQueueName);
builder.Services.AddQueuePublisher<StationImageMessage>(o => o.ImagesQueueName);
builder.Services.Configure<QueueJobOptions>(builder.Configuration.GetSection(QueueJobOptions.SectionName));

builder.Services.AddHttpClient<IStationService, StationService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherAPI-ImageStarter/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<StartGenerationJob>();

await builder.Build().RunAsync();
