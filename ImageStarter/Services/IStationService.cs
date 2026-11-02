using ImageStarter.DTOs;

namespace ImageStarter.Services;

public interface IStationService
{
    Task<List<WeatherStation>?> GetStations();
}