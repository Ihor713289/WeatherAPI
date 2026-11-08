using Azure;
using Azure.Data.Tables;

namespace WeatherAPI.Shared.Tables;

public interface ICreationProcessRepository
{
    Task<CreationProcess> CreateAsync(string processId, CancellationToken cancellationToken = default);

    Task<CreationProcess?> GetAsync(string processId, CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(string processId, int expectedImages, CancellationToken cancellationToken = default);

    Task SetStatusAsync(string processId, CreationStatus status, string? error = null,
        CancellationToken cancellationToken = default);
}

public sealed class CreationProcessRepository(TableClient table) : ICreationProcessRepository
{
    public async Task<CreationProcess> CreateAsync(string processId, CancellationToken cancellationToken = default)
    {
        var process = new CreationProcess
        {
            PartitionKey = processId,
            RowKey = processId,
            Status = CreationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var response = await table.AddEntityAsync(process, cancellationToken);
        process.ETag = response.Headers.ETag ?? default;
        return process;
    }

    public async Task<CreationProcess?> GetAsync(string processId, CancellationToken cancellationToken = default)
    {
        var response = await table.GetEntityIfExistsAsync<CreationProcess>(processId, processId,
            cancellationToken: cancellationToken);
        return response.HasValue ? response.Value : null;
    }

    public Task MarkProcessingAsync(string processId, int expectedImages, CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(processId, processId)
        {
            [nameof(CreationProcess.Status)] = CreationStatus.Processing.ToString(),
            [nameof(CreationProcess.ExpectedImages)] = expectedImages
        };
        return table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);
    }

    public Task SetStatusAsync(string processId, CreationStatus status, string? error = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(processId, processId)
        {
            [nameof(CreationProcess.Status)] = status.ToString(),
            [nameof(CreationProcess.Error)] = error
        };
        return table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);
    }
}
