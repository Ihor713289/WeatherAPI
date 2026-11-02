namespace WeatherAPI.Repositories.Blobs;

public interface IImageBlobRepository
{
    public Task<string> UploadImageAsync(Stream imageStream, string fileName);
    public Task DeleteImageAsync(string imageUrl);
    public Task<string> GetImageSasUrlAsync(string imageUrl);
    public Task<List<string>> GetImageSasUrlsAsync(List<string> imageUrls);
}