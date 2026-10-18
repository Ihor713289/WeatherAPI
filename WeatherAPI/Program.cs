using WeatherAPI.Messaging;
using WeatherAPI.Repositories.Tables;
using WeatherAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddSingleton<IQueuePublisher, AzureQueuePublisher>();
builder.Services.AddSingleton<ICreationProcessRepository, CreationProcessRepository>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();