using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using WeatherAPI.Repositories.Blobs;
using WeatherAPI.Services;
using WeatherAPI.Shared;
using WeatherAPI.Shared.Blobs;
using WeatherAPI.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddWeatherStorage(builder.Configuration);
builder.Services.AddQueuePublisher<StartGenerationMessage>(o => o.ProcessesQueueName);
builder.Services.AddBlobContainer(BlobContainerKeys.Images, o => o.ImagesContainerName);
builder.Services.AddSingleton<IImageBlobRepository, ImageBlobRepository>();
builder.Services.AddScoped<IImageService, ImageService>();

// Container Apps terminates TLS at the ingress and forwards plain HTTP; honour X-Forwarded-Proto/Host
// so generated status links use the public https URL.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto |
                         ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();
