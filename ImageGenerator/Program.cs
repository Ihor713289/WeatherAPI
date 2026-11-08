using ImageGenerator;
using ImageGenerator.Helpers;
using ImageGenerator.Services;
using WeatherAPI.Shared;
using WeatherAPI.Shared.Blobs;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Contracts;

// Runs as an event-driven Azure Container Apps Job: processes the images queue, then exits.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWeatherStorage(builder.Configuration);
builder.Services.AddQueueReader<StationImageMessage>(o => o.ImagesQueueName);
builder.Services.AddBlobContainer(BlobContainerKeys.Images, o => o.ImagesContainerName);
builder.Services.AddBlobContainer(BlobContainerKeys.BackgroundCache, o => o.BackgroundCacheContainerName);
builder.Services.Configure<QueueJobOptions>(builder.Configuration.GetSection(QueueJobOptions.SectionName));

void ConfigureClient(HttpClient client)
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherAPI-ImageGenerator/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
}

builder.Services.AddHttpClient<IUnsplashHelper, UnsplashHelper>(ConfigureClient);
builder.Services.AddHttpClient(ImageService.HttpClientName, ConfigureClient);
builder.Services.AddSingleton<IBackgroundProvider, CachedBackgroundProvider>();
builder.Services.AddSingleton<IImageService, ImageService>();

builder.Services.AddHostedService<ImageGenerationJob>();

await builder.Build().RunAsync();
