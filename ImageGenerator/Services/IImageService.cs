using WeatherAPI.Shared.Contracts;

namespace ImageGenerator.Services;

public interface IImageService
{
    /// <summary>
    /// Renders the weather image for <paramref name="station"/> and stores it as
    /// "&lt;processId&gt;/&lt;stationId&gt;.jpg" (overwriting on retries). Returns the blob name.
    /// </summary>
    Task<string> GenerateImageAsync(string processId, WeatherStation station,
        CancellationToken cancellationToken = default);
}
