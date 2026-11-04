using ImageStarter.DTOs;

namespace ImageGenerator.Services;

public interface IImageService
{
    Task<string> GenerateImageAsync(WeatherStation station);
}