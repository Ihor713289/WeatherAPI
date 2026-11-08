using WeatherAPI.DTOs;

namespace WeatherAPI.Services;

public interface IImageService
{
    /// <summary>Creates a generation process and queues it. Returns immediately; work happens in the jobs.</summary>
    Task<GenerationResponse> StartGenerationAsync(Func<string, string> statusUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the status and (SAS) image links of a process, or null if it does not exist.</summary>
    Task<FullGenerationResponse?> GetGenerationAsync(string id, CancellationToken cancellationToken = default);
}
