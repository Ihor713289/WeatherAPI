using Azure.Storage.Blobs;
using ImageGenerator;
using ImageGenerator.Helpers;
using ImageGenerator.Services;

var builder = Host.CreateApplicationBuilder(args);

var storageConnectionString = builder.Configuration["BlobConfig:ConnectionString"]
                              ?? throw new InvalidOperationException("Connection string 'Storage' is missing.");

builder.Services.AddSingleton(new BlobServiceClient(storageConnectionString));

builder.Services.AddKeyedSingleton(ImageService.ImagesContainerKey, (sp, _) =>
    CreateContainer(sp, builder.Configuration["BlobConfig:ContainerName"] ?? "images"));

builder.Services.AddKeyedSingleton(ImageService.CacheContainerKey, (sp, _) =>
    CreateContainer(sp, builder.Configuration["BlobConfig:CacheContainer"] ?? "temp-backgrounds"));

void ConfigureClient(HttpClient client)
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherAPI-ImageGenerator/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
}

builder.Services.AddHttpClient<IUnsplashHelper, UnsplashHelper>(ConfigureClient);
builder.Services.AddHttpClient(nameof(ImageService), ConfigureClient);
builder.Services.AddTransient<IImageService, ImageService>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static BlobContainerClient CreateContainer(IServiceProvider sp, string name)
{
    var container = sp.GetRequiredService<BlobServiceClient>().GetBlobContainerClient(name);
    container.CreateIfNotExists();
    return container;
}
