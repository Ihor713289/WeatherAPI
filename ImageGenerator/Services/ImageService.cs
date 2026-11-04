using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageGenerator.Helpers;
using ImageStarter.DTOs;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageGenerator.Services;

public class ImageService : IImageService
{
    public const string ImagesContainerKey = "images";

    public const string CacheContainerKey = "cache";

    private readonly HttpClient _httpClient;
    private readonly IUnsplashHelper _unsplash;
    private readonly BlobContainerClient _imagesContainer;
    private readonly BlobContainerClient _cacheContainer;
    private readonly ILogger<ImageService> _logger;

    public ImageService(
        IHttpClientFactory httpClientFactory,
        IUnsplashHelper unsplash,
        [FromKeyedServices(ImagesContainerKey)] BlobContainerClient imagesContainer,
        [FromKeyedServices(CacheContainerKey)] BlobContainerClient cacheContainer,
        ILogger<ImageService> logger)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(ImageService));
        _unsplash = unsplash;
        _imagesContainer = imagesContainer;
        _cacheContainer = cacheContainer;
        _logger = logger;
    }

    public async Task<string> GenerateImageAsync(WeatherStation station)
    {
        ArgumentNullException.ThrowIfNull(station);

        var background = await GetBackgroundAsync(station);

        using var image = Image.Load<Rgba32>(background);
        using var icon = await TryDownloadIconAsync(station.IconUrl);

        DrawWeatherData(image, station, icon);

        using var output = new MemoryStream();
        await image.SaveAsPngAsync(output);
        output.Position = 0;

        return await UploadAsync(station, output);
    }

    private async Task<byte[]> GetBackgroundAsync(WeatherStation station)
    {
        var cacheBlob = _cacheContainer.GetBlobClient(GetCacheBlobName(station));

        try
        {
            var cached = await cacheBlob.DownloadContentAsync();
            _logger.LogInformation("Cache hit for station {StationName} ({Region})", station.Name, station.Region);
            return cached.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Cache miss for station {StationName} ({Region}), querying Unsplash",
                station.Name, station.Region);
        }

        var photo = await _unsplash.GetLocationImageAsync(station);

        try
        {
            await cacheBlob.UploadAsync(BinaryData.FromBytes(photo.Content), new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" },
                Metadata = new Dictionary<string, string>
                {
                    ["stationname"] = Uri.EscapeDataString(station.Name),
                    ["region"] = Uri.EscapeDataString(station.Region),
                    ["unsplashphotoid"] = Uri.EscapeDataString(photo.PhotoId),
                    ["photographer"] = Uri.EscapeDataString(photo.Photographer)
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            });
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            _logger.LogDebug("Background for {StationName} was cached concurrently by another worker", station.Name);
        }

        return photo.Content;
    }

    private static string GetCacheBlobName(WeatherStation station) =>
        $"{Slugify(station.Region)}/{Slugify(station.Name)}.jpg";

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var slug = new string(value.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }

        return slug.Trim('-') is { Length: > 0 } result ? result : "unknown";
    }

    private async Task<Image<Rgba32>> DownloadImageAsync(string url)
    {
        await using var stream = await _httpClient.GetStreamAsync(url);
        return await Image.LoadAsync<Rgba32>(stream);
    }

    private async Task<Image<Rgba32>?> TryDownloadIconAsync(string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(iconUrl))
        {
            return null;
        }

        try
        {
            return await DownloadImageAsync(iconUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or UnknownImageFormatException or InvalidImageContentException)
        {
            _logger.LogWarning(ex, "Could not load weather icon from {IconUrl}", iconUrl);
            return null;
        }
    }


    private static void DrawWeatherData(Image<Rgba32> image, WeatherStation station, Image<Rgba32>? icon)
    {
        var padding = image.Width * 0.03f;
        var iconSize = (int)(image.Width * 0.12f);
        var temperatureFont = ResolveFont(image.Width * 0.08f, FontStyle.Bold);
        var nameFont = ResolveFont(image.Width * 0.035f, FontStyle.Regular);

        var temperatureText = station.Temperature.ToString("0.0", CultureInfo.InvariantCulture) + " °C";
        var nameText = station.Name;

        var temperatureSize = TextMeasurer.MeasureSize(temperatureText, new TextOptions(temperatureFont));
        var nameSize = TextMeasurer.MeasureSize(nameText, new TextOptions(nameFont));

        var textX = padding * 2 + (icon is null ? 0 : iconSize + padding);
        var textBlockHeight = temperatureSize.Height + padding / 2 + nameSize.Height;
        var contentHeight = Math.Max(icon is null ? 0 : iconSize, textBlockHeight);
        var panelWidth = textX + Math.Max(temperatureSize.Width, nameSize.Width) + padding;
        var panelHeight = contentHeight + padding * 2;
        var panel = new RectangularPolygon(padding, padding, panelWidth, panelHeight);

        icon?.Mutate(i => i.Resize(iconSize, iconSize));

        image.Mutate(ctx =>
        {
            ctx.Fill(Color.Black.WithAlpha(0.55f), panel);

            if (icon is not null)
            {
                var iconY = padding * 2 + (contentHeight - iconSize) / 2;
                ctx.DrawImage(icon, new Point((int)(padding * 2), (int)iconY), 1f);
            }

            var textY = padding * 2 + (contentHeight - textBlockHeight) / 2;

            ctx.DrawText(
                new RichTextOptions(temperatureFont) { Origin = new PointF(textX, textY) },
                temperatureText,
                Color.White);

            ctx.DrawText(
                new RichTextOptions(nameFont) { Origin = new PointF(textX, textY + temperatureSize.Height + padding / 2) },
                nameText,
                Color.LightGray);
        });
    }

    private static Font ResolveFont(float size, FontStyle style)
    {
        foreach (var name in new[] { "Verdana", "DejaVu Sans", "Liberation Sans", "Arial" })
        {
            if (SystemFonts.TryGet(name, out var family))
            {
                return family.CreateFont(size, style);
            }
        }

        if (!SystemFonts.Families.Any())
        {
            throw new InvalidOperationException(
                "No system fonts installed. Install a font package (e.g. fonts-dejavu-core) in the container.");
        }

        return SystemFonts.Families.First().CreateFont(size, style);
    }

    private async Task<string> UploadAsync(WeatherStation station, Stream content)
    {
        var blobName = $"{station.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.png";
        var blobClient = _imagesContainer.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
        });

        _logger.LogInformation("Uploaded image for station {StationId} to {BlobUri}", station.Id, blobClient.Uri);
        return blobClient.Uri.ToString();
    }
}
