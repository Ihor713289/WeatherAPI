namespace WeatherAPI.Repositories.Blobs;

public interface IImageBlobRepository
{
    /// <summary>Returns a time-limited, read-only SAS URL for a blob in the (private) images container.</summary>
    Uri GetReadUrl(string blobName);
}
