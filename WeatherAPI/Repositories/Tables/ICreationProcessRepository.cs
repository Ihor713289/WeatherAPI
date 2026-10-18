using WeatherAPI.DTOs;
using WeatherAPI.Models;

namespace WeatherAPI.Repositories.Tables;

public interface ICreationProcessRepository
{
    public CreationProcess Generate(string id);
    public Task<CreationProcess> GetCreationProcess(string id);
}