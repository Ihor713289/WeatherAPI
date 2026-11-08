namespace WeatherAPI.Shared.Configuration;

/// <summary>
/// Storage account settings shared by the API and both jobs (bound from the "Storage" section).
/// In Azure only <see cref="ConnectionString"/> has to be provided (env var <c>Storage__ConnectionString</c>).
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;

    public string ProcessesQueueName { get; set; } = "generation-processes";

    public string ImagesQueueName { get; set; } = "weather-images";

    public string ProcessesTableName { get; set; } = "CreationProcesses";

    public string ImagesTableName { get; set; } = "GeneratedImages";

    public string ImagesContainerName { get; set; } = "weather-images";

    public string BackgroundCacheContainerName { get; set; } = "temp-backgrounds";
}
