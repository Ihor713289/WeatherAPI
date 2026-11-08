namespace WeatherAPI.Shared.Contracts;

/// <summary>Message on the processes queue: start fetching stations for a generation process.</summary>
public sealed record StartGenerationMessage(string ProcessId);

/// <summary>Message on the images queue: render and store the image for one station.</summary>
public sealed record StationImageMessage(string ProcessId, WeatherStation Station);
