using WeatherAPI.Models;

namespace WeatherAPI.Repositories.Tables;

public interface IGeneratedImageRepository
{
    public Task<List<GeneratedImage>> GetGeneratedImages(string processId);
}