using Azure.Data.Tables;
using WeatherAPI.DTOs;
using WeatherAPI.Enums;
using WeatherAPI.Models;

namespace WeatherAPI.Repositories.Tables;

public class CreationProcessRepository: ICreationProcessRepository
{
    private TableClient _tableClient;
    public CreationProcessRepository(IConfiguration configuration)
    {
        var service = new TableServiceClient(configuration["TableConfig:ConnectionString"]);
        _tableClient = service.GetTableClient(configuration["TableConfig:CreationProcessTableName"]);
        _tableClient.CreateIfNotExists();
    }

    public CreationProcess Generate(string id)
    {
        CreationProcess creation = new CreationProcess("imagegeneration", id, CreationStatus.Pending);
        _tableClient.AddEntity(creation);
        return creation;
    }

    public async Task<CreationProcess> GetCreationProcess(string id)
    {
        return await _tableClient.GetEntityAsync<CreationProcess>(id, id);
    }
}