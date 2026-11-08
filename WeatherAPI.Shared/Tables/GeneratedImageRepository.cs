using System.Globalization;
using Azure.Data.Tables;
using WeatherAPI.Shared.Contracts;

namespace WeatherAPI.Shared.Tables;

public interface IGeneratedImageRepository
{
    Task<IReadOnlyList<GeneratedImage>> GetByProcessAsync(string processId, CancellationToken cancellationToken = default);

    /// <summary>Adds a Pending row for every station that does not have a row yet (safe to call again on retries).</summary>
    Task AddPendingAsync(string processId, IReadOnlyCollection<WeatherStation> stations,
        CancellationToken cancellationToken = default);

    Task SetStatusAsync(string processId, int stationId, ImageStatus status, string? blobName = null,
        string? error = null, CancellationToken cancellationToken = default);
}

public sealed class GeneratedImageRepository(TableClient table) : IGeneratedImageRepository
{
    // Entity group transactions are limited to 100 operations within a single partition.
    private const int MaxBatchSize = 100;

    public async Task<IReadOnlyList<GeneratedImage>> GetByProcessAsync(string processId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GeneratedImage>();
        await foreach (var image in table.QueryAsync<GeneratedImage>(x => x.PartitionKey == processId,
                           cancellationToken: cancellationToken))
        {
            results.Add(image);
        }

        return results;
    }

    public async Task AddPendingAsync(string processId, IReadOnlyCollection<WeatherStation> stations,
        CancellationToken cancellationToken = default)
    {
        var existing = (await GetByProcessAsync(processId, cancellationToken))
            .Select(i => i.RowKey)
            .ToHashSet();

        var actions = stations
            .Select(s => new GeneratedImage
            {
                PartitionKey = processId,
                RowKey = ToRowKey(s.Id),
                StationName = s.Name,
                Status = ImageStatus.Pending
            })
            .Where(e => existing.Add(e.RowKey)) // also de-duplicates stations within the list
            .Select(e => new TableTransactionAction(TableTransactionActionType.Add, e));

        foreach (var batch in actions.Chunk(MaxBatchSize))
        {
            await table.SubmitTransactionAsync(batch, cancellationToken);
        }
    }

    public Task SetStatusAsync(string processId, int stationId, ImageStatus status, string? blobName = null,
        string? error = null, CancellationToken cancellationToken = default)
    {
        var entity = new TableEntity(processId, ToRowKey(stationId))
        {
            [nameof(GeneratedImage.Status)] = status.ToString(),
            [nameof(GeneratedImage.Error)] = error
        };

        if (blobName is not null)
        {
            entity[nameof(GeneratedImage.BlobName)] = blobName;
        }

        // Upsert so a station message is still tracked even if its Pending row is somehow missing.
        return table.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
    }

    private static string ToRowKey(int stationId) => stationId.ToString(CultureInfo.InvariantCulture);
}
