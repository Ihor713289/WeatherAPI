using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using WeatherAPI.Shared.Blobs;

namespace WeatherAPI.Repositories.Blobs;

/// <summary>
/// The images container is private; clients get short-lived SAS URLs instead of anonymous blob access.
/// SAS generation needs a shared-key connection string (AccountKey=...).
/// </summary>
public sealed class ImageBlobRepository(
    [FromKeyedServices(BlobContainerKeys.Images)] BlobContainerClient container,
    IConfiguration configuration) : IImageBlobRepository
{
    private readonly TimeSpan _lifetime =
        TimeSpan.FromMinutes(configuration.GetValue<int?>("SasTokenExpirationMinutes") ?? 60);

    public Uri GetReadUrl(string blobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        var blob = container.GetBlobClient(blobName);
        if (!blob.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Cannot generate SAS URLs: the storage connection string must contain an account key.");
        }

        return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(_lifetime));
    }
}
