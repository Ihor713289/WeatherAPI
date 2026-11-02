using System.Net.Http.Json;
using ImageStarter.DTOs;

namespace ImageStarter.Services;

public class StationService : IStationService
{
    public async Task<List<WeatherStation>?> GetStations()
    {
        var response = await new HttpClient().GetAsync("https://data.buienradar.nl/2.0/feed/json");
        return await response.Content.ReadFromJsonAsync<List<WeatherStation>>();
    }
}