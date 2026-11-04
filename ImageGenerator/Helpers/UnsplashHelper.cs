using System.Net.Http.Headers;
using System.Text.Json;
using ImageStarter.DTOs;

namespace ImageGenerator.Helpers;

public record UnsplashImage(string PhotoId, string Photographer, byte[] Content);

public interface IUnsplashHelper
{
    Task<UnsplashImage> GetLocationImageAsync(WeatherStation station, CancellationToken cancellationToken = default);
}

public class UnsplashHelper : IUnsplashHelper
{
    private const string SearchEndpoint = "https://api.unsplash.com/search/photos";
    private const string FallbackQuery = "Netherlands map";
    private const int TargetWidth = 1080;

    private readonly HttpClient _httpClient;
    private readonly ILogger<UnsplashHelper> _logger;
    private readonly string _accessKey;

    public UnsplashHelper(HttpClient httpClient, IConfiguration configuration, ILogger<UnsplashHelper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _accessKey = configuration["Unsplash:AccessKey"]
                     ?? throw new InvalidOperationException("Configuration value 'Unsplash:AccessKey' is missing.");
    }

    public async Task<UnsplashImage> GetLocationImageAsync(WeatherStation station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);

        var queries = new[]
            {
                $"{station.Region} map",
                $"{station.Region} netherlands",
                FallbackQuery
            }
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct();

        foreach (var query in queries)
        {
            var photo = await SearchAsync(query, cancellationToken);
            if (photo is null)
            {
                _logger.LogInformation("No Unsplash results for '{Query}', trying next query", query);
                continue;
            }

            var content = await _httpClient.GetByteArrayAsync(photo.Value.Url, cancellationToken);
            return new UnsplashImage(photo.Value.Id, photo.Value.Photographer, content);
        }

        throw new InvalidOperationException($"No Unsplash image found for station {station.Id} ({station.Name}).");
    }

    private async Task<(string Id, string Photographer, string Url)?> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var requestUri = $"{SearchEndpoint}?query={Uri.EscapeDataString(query)}&per_page=10&orientation=landscape";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", _accessKey);
        request.Headers.Add("Accept-Version", "v1");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = json.RootElement.GetProperty("results");
        if (results.GetArrayLength() == 0)
        {
            return null;
        }

        var photo = results[Random.Shared.Next(results.GetArrayLength())];
        var rawUrl = photo.GetProperty("urls").GetProperty("raw").GetString();
        if (rawUrl is null)
        {
            return null;
        }

        var id = photo.GetProperty("id").GetString() ?? string.Empty;
        var photographer = photo.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var name)
            ? name.GetString() ?? string.Empty
            : string.Empty;

        return (id, photographer, $"{rawUrl}&w={TargetWidth}&fm=jpg&q=80&fit=max");
    }
}
