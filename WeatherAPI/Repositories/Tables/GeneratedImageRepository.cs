using Azure.Data.Tables;
using WeatherAPI.Models;

namespace WeatherAPI.Repositories.Tables;

public class GeneratedImageRepository: IGeneratedImageRepository
{
    private TableClient _tableClient;
    public GeneratedImageRepository(IConfiguration configuration)
    {
        var service = new TableServiceClient(configuration["TableConfig:ConnectionString"]);
        _tableClient = service.GetTableClient(configuration["TableConfig:GeneratedImagesTableName"]);
        _tableClient.CreateIfNotExists();
    }
    public async Task<List<GeneratedImage>> GetGeneratedImages(string processId)
    {
        return await _tableClient.QueryAsync<GeneratedImage>(x => x.PartitionKey == processId).ToListAsync();
    }
}