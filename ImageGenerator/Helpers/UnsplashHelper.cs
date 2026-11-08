using System.Net.Http.Headers;
using System.Text.Json;
using WeatherAPI.Shared.Contracts;

namespace ImageGenerator.Helpers;

public record UnsplashImage(string PhotoId, string Photographer, byte[] Content);

public interface IUnsplashHelper
{
    /// <summary>Returns a photo for the station's region, or null when Unsplash has no matching photo.</summary>
    Task<UnsplashImage?> GetLocationImageAsync(WeatherStation station, CancellationToken cancellationToken = default);
}

public class UnsplashHelper : IUnsplashHelper
{
    private const string SearchEndpoint = "https://api.unsplash.com/search/photos";
    private const string FallbackQuery = "Netherlands landscape";
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

    public async Task<UnsplashImage?> GetLocationImageAsync(WeatherStation station,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);

        // Demo apps get 50 API requests per hour, so keep the number of searches per station minimal.
        var queries = new[] { $"{station.Region} Netherlands", FallbackQuery }
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

            // Downloads from images.unsplash.com don't count against the API rate limit.
            var content = await _httpClient.GetByteArrayAsync(photo.Value.Url, cancellationToken);
            return new UnsplashImage(photo.Value.Id, photo.Value.Photographer, content);
        }

        return null;
    }

    private async Task<(string Id, string Photographer, string Url)?> SearchAsync(string query,
        CancellationToken cancellationToken)
    {
        var requestUri = $"{SearchEndpoint}?query={Uri.EscapeDataString(query)}&per_page=10&orientation=landscape";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Client-ID", _accessKey);
        request.Headers.Add("Accept-Version", "v1");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            return null;
        }

        var photo = results[Random.Shared.Next(results.GetArrayLength())];
        var rawUrl = photo.TryGetProperty("urls", out var urls) && urls.TryGetProperty("raw", out var raw)
            ? raw.GetString()
            : null;
        if (rawUrl is null)
        {
            return null;
        }

        var id = photo.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var photographer = photo.TryGetProperty("user", out var user) && user.TryGetProperty("name", out var name)
            ? name.GetString() ?? string.Empty
            : string.Empty;

        // The raw URL already contains a query string (ixid=...), so append with '&'.
        return (id, photographer, $"{rawUrl}&w={TargetWidth}&fm=jpg&q=80&fit=max");
    }
}
