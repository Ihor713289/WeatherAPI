using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace WeatherAPI.Repositories.Blobs;

public class ImageBlobRepository : IImageBlobRepository
{
    private readonly BlobContainerClient _containerClient;
    private readonly int _sasTokenExpirationMinutes;

    public ImageBlobRepository(IConfiguration configuration)
    {
        var service = new BlobServiceClient(configuration["BlobConfig:ConnectionString"]);
        _containerClient = service.GetBlobContainerClient(configuration["BlobConfig:ContainerName"]);
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);

        _sasTokenExpirationMinutes = configuration.GetValue<int?>("BlobConfig:SasTokenExpirationMinutes") ?? 60;
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
    {
        if (imageStream is null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        var blobClient = _containerClient.GetBlobClient(fileName);

        await blobClient.UploadAsync(
            imageStream,
            new BlobHttpHeaders
            {
                ContentType = GetContentType(fileName)
            });

        return blobClient.Uri.ToString();
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image url is required.", nameof(imageUrl));
        }

        var blobClient = GetBlobClientFromUrl(imageUrl);
        await blobClient.DeleteIfExistsAsync();
    }

    public Task<string> GetImageSasUrlAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            throw new ArgumentException("Image url is required.", nameof(imageUrl));
        }

        var blobClient = GetBlobClientFromUrl(imageUrl);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("Cannot generate SAS URL. The blob client must be created with shared key credentials.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobClient.Name,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_sasTokenExpirationMinutes)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }

    public async Task<List<string>> GetImageSasUrlsAsync(List<string> imageUrls)
    {
        if (imageUrls is null)
        {
            throw new ArgumentNullException(nameof(imageUrls));
        }

        var sasUrls = new List<string>();

        foreach (var imageUrl in imageUrls)
        {
            sasUrls.Add(await GetImageSasUrlAsync(imageUrl));
        }

        return sasUrls;
    }

    private BlobClient GetBlobClientFromUrl(string imageUrl)
    {
        var blobUri = new Uri(imageUrl);
        var blobName = Uri.UnescapeDataString(blobUri.Segments.Last());

        return _containerClient.GetBlobClient(blobName);
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}