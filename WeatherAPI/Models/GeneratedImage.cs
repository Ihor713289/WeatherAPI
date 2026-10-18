using Azure;
using Azure.Data.Tables;
using WeatherAPI.Enums;

namespace WeatherAPI.Models;

public class GeneratedImage: ITableEntity
{
    public GeneratedImage() {}
    
    public GeneratedImage(string partitionKey, string rowKey, string stationName, string imageUrl, ImageStatus status)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
        StationName = stationName;
        ImageUrl = imageUrl;
        Status = status;
    }
    
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string StationName { get; set; }
    public string ImageUrl { get; set; }
    public string SasUrl { get; set; }
    public ImageStatus Status { get; set; }
}