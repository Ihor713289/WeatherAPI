using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using WeatherAPI.Shared.Blobs;
using WeatherAPI.Shared.Contracts;

namespace ImageGenerator.Services;

/// <summary>Orchestrates one station image: background + icon → render → upload.</summary>
public class ImageService : IImageService
{
    public const string HttpClientName = nameof(ImageService);

    private static readonly JpegEncoder Encoder = new() { Quality = 85 };

    private readonly HttpClient _httpClient;
    private readonly IBackgroundProvider _backgrounds;
    private readonly BlobContainerClient _imagesContainer;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IHttpClientFactory httpClientFactory,
        IBackgroundProvider backgrounds,
        [FromKeyedServices(BlobContainerKeys.Images)] BlobContainerClient imagesContainer,
        ILogger<ImageService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _backgrounds = backgrounds;
        _imagesContainer = imagesContainer;
        _logger = logger;
    }

    public async Task<string> GenerateImageAsync(string processId, WeatherStation station,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        ArgumentNullException.ThrowIfNull(station);

        // Fetch both in parallel; make sure the icon is disposed even if the background fails.
        var iconTask = TryDownloadIconAsync(station.IconUrl, cancellationToken);
        Background background;
        try
        {
            background = await _backgrounds.GetBackgroundAsync(station, cancellationToken);
        }
        catch
        {
            try
            {
                (await iconTask)?.Dispose();
            }
            catch
            {
                // Ignore: the background failure is the one worth reporting.
            }

            throw;
        }

        using (background)
        using (var icon = await iconTask)
        {
            WeatherImageRenderer.Render(background.Image, station, icon, background.Attribution);

            using var output = new MemoryStream();
            await background.Image.SaveAsJpegAsync(output, Encoder, cancellationToken);
            output.Position = 0;

            return await UploadAsync(processId, station, output, cancellationToken);
        }
    }

    private async Task<Image<Rgba32>?> TryDownloadIconAsync(string? iconUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return null;
        }

        try
        {
            await using var stream = await _httpClient.GetStreamAsync(iconUrl, cancellationToken);
            return await Image.LoadAsync<Rgba32>(stream, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or ImageFormatException
                                       // HttpClient timeouts surface as TaskCanceledException.
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "Could not load weather icon from {IconUrl}, rendering without it", iconUrl);
            return null;
        }
    }

    private async Task<string> UploadAsync(string processId, WeatherStation station, Stream content,
        CancellationToken cancellationToken)
    {
        // Deterministic name: a redelivered queue message overwrites the same blob instead of adding a duplicate.
        var blobName = $"{processId}/{station.Id}.jpg";
        var blobClient = _imagesContainer.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
        }, cancellationToken);

        _logger.LogInformation("Uploaded image for station {StationId} ({StationName}) to {BlobName}",
            station.Id, station.Name, blobName);
        return blobName;
    }
}
