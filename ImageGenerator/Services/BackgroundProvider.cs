using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageGenerator.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WeatherAPI.Shared.Blobs;
using WeatherAPI.Shared.Contracts;

namespace ImageGenerator.Services;

/// <summary>A decoded background plus the photo credit to print on it (null for the generated fallback).</summary>
public sealed record Background(Image<Rgba32> Image, string? Attribution) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

public interface IBackgroundProvider
{
    Task<Background> GetBackgroundAsync(WeatherStation station, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gets a background per region: from the blob cache if present, otherwise from Unsplash (and caches it).
/// If Unsplash is unavailable (e.g. rate limited) a generated gradient is used, so the image still gets made.
/// </summary>
public sealed class CachedBackgroundProvider(
    IUnsplashHelper unsplash,
    [FromKeyedServices(BlobContainerKeys.BackgroundCache)] BlobContainerClient cache,
    ILogger<CachedBackgroundProvider> logger) : IBackgroundProvider
{
    private const string PhotographerMetadataKey = "photographer";

    public async Task<Background> GetBackgroundAsync(WeatherStation station,
        CancellationToken cancellationToken = default)
    {
        var blob = cache.GetBlobClient($"{Slug.Create(station.Region)}.jpg");

        var cached = await TryLoadCachedAsync(blob, station, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        UnsplashImage? photo;
        try
        {
            photo = await unsplash.GetLocationImageAsync(station, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Unsplash request for {Region} failed, using generated background", station.Region);
            return CreateFallback();
        }

        if (photo is null)
        {
            logger.LogWarning("Unsplash returned no photo for {Region}, using generated background", station.Region);
            return CreateFallback();
        }

        var image = Image.Load<Rgba32>(photo.Content);
        await TryCacheAsync(blob, station, photo, cancellationToken);
        return new Background(image, FormatAttribution(photo.Photographer));
    }

    private async Task<Background?> TryLoadCachedAsync(BlobClient blob, WeatherStation station,
        CancellationToken cancellationToken)
    {
        try
        {
            var cached = await blob.DownloadContentAsync(cancellationToken);
            var image = Image.Load<Rgba32>(cached.Value.Content.ToMemory().Span);
            cached.Value.Details.Metadata.TryGetValue(PhotographerMetadataKey, out var photographer);

            logger.LogInformation("Background cache hit for {Region}", station.Region);
            return new Background(image, FormatAttribution(Uri.UnescapeDataString(photographer ?? string.Empty)));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            logger.LogInformation("Background cache miss for {Region}, querying Unsplash", station.Region);
            return null;
        }
        catch (ImageFormatException ex)
        {
            // Don't let a corrupt cache entry fail every future image for this region.
            logger.LogWarning(ex, "Cached background for {Region} is corrupt, replacing it", station.Region);
            await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return null;
        }
    }

    private async Task TryCacheAsync(BlobClient blob, WeatherStation station, UnsplashImage photo,
        CancellationToken cancellationToken)
    {
        try
        {
            await blob.UploadAsync(BinaryData.FromBytes(photo.Content), new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" },
                // Metadata values must be ASCII, hence the escaping.
                Metadata = new Dictionary<string, string>
                {
                    ["region"] = Uri.EscapeDataString(station.Region),
                    ["unsplashphotoid"] = Uri.EscapeDataString(photo.PhotoId),
                    [PhotographerMetadataKey] = Uri.EscapeDataString(photo.Photographer)
                },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
            }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            logger.LogDebug("Background for {Region} was cached concurrently by another execution", station.Region);
        }
    }

    private static string? FormatAttribution(string photographer) =>
        string.IsNullOrWhiteSpace(photographer) ? "Photo: Unsplash" : $"Photo: {photographer} / Unsplash";

    private static Background CreateFallback()
    {
        var image = new Image<Rgba32>(1080, 720);
        image.Mutate(ctx => ctx.Fill(new LinearGradientBrush(
            new PointF(0, 0),
            new PointF(image.Width, image.Height),
            GradientRepetitionMode.None,
            new ColorStop(0, Color.ParseHex("#1e3c72")),
            new ColorStop(1, Color.ParseHex("#6dd5ed")))));
        return new Background(image, null);
    }
}

internal static class Slug
{
    public static string Create(string? value)
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
}
