using Azure;
using Azure.Data.Tables;
using WeatherAPI.Enums;

namespace WeatherAPI.Models;

public class CreationProcess : ITableEntity
{
    public CreationProcess() {}

    public CreationProcess(string partitionKey, string rowKey, CreationStatus status)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
        Status = status;
    }

    public CreationProcess(string partitionKey, string rowKey, CreationStatus status, ETag etag,
        DateTimeOffset timestamp): this(partitionKey, rowKey, status)
    {
        ETag = etag;
        Timestamp = timestamp;
    }
    
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public CreationStatus Status { get; set; }
}