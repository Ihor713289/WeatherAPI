using System.Net.Http.Json;
using ImageStarter.DTOs;
using WeatherAPI.Shared.Contracts;

namespace ImageStarter.Services;

public interface IStationService
{
    Task<IReadOnlyList<WeatherStation>> GetStationsAsync(CancellationToken cancellationToken = default);
}

public sealed class StationService(HttpClient httpClient) : IStationService
{
    public const string FeedUrl = "https://data.buienradar.nl/2.0/feed/json";

    public async Task<IReadOnlyList<WeatherStation>> GetStationsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<BuienradarResponse>(FeedUrl, cancellationToken);

        return (response?.Actual?.StationMeasurements ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.StationName))
            .DistinctBy(s => s.StationId)
            .Select(s => new WeatherStation(
                s.StationId,
                s.StationName!,
                string.IsNullOrWhiteSpace(s.Region) ? s.StationName! : s.Region,
                s.Latitude,
                s.Longitude,
                s.Temperature,
                s.WeatherDescription,
                // The 96x96 icon looks much better on a 1080px image than the 30x30 one.
                s.FullIconUrl ?? s.IconUrl))
            .ToList();
    }
}
