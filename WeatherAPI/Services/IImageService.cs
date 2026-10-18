using WeatherAPI.DTOs;

namespace WeatherAPI.Services;

public interface IImageService
{
    public Task<GenerationResponse> Generate();
}