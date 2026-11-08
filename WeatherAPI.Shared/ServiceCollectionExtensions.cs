using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WeatherAPI.Shared.Configuration;
using WeatherAPI.Shared.Queues;
using WeatherAPI.Shared.Tables;

namespace WeatherAPI.Shared;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="StorageOptions"/>, the storage service clients and the table repositories.
    /// Tables, queues and containers are created on first use if they don't exist (Bicep normally creates them).
    /// </summary>
    public static IServiceCollection AddWeatherStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                $"{StorageOptions.SectionName}:{nameof(StorageOptions.ConnectionString)} is required.")
            .ValidateOnStart();

        services.AddSingleton(sp => new TableServiceClient(Options(sp).ConnectionString));
        services.AddSingleton(sp => new QueueServiceClient(Options(sp).ConnectionString));
        services.AddSingleton(sp => new BlobServiceClient(Options(sp).ConnectionString));

        services.AddSingleton<ICreationProcessRepository>(sp =>
            new CreationProcessRepository(CreateTable(sp, Options(sp).ProcessesTableName)));
        services.AddSingleton<IGeneratedImageRepository>(sp =>
            new GeneratedImageRepository(CreateTable(sp, Options(sp).ImagesTableName)));

        return services;
    }

    public static IServiceCollection AddQueuePublisher<T>(this IServiceCollection services,
        Func<StorageOptions, string> queueName)
    {
        services.AddSingleton<IQueuePublisher<T>>(sp =>
            new AzureQueuePublisher<T>(CreateQueue(sp, queueName(Options(sp)))));
        return services;
    }

    public static IServiceCollection AddQueueReader<T>(this IServiceCollection services,
        Func<StorageOptions, string> queueName)
    {
        services.AddSingleton<IQueueReader<T>>(sp =>
        {
            var name = queueName(Options(sp));
            return new AzureQueueReader<T>(CreateQueue(sp, name), CreateQueue(sp, $"{name}-poison"));
        });
        return services;
    }

    /// <summary>Registers a private (no anonymous access) blob container as a keyed service.</summary>
    public static IServiceCollection AddBlobContainer(this IServiceCollection services, string key,
        Func<StorageOptions, string> containerName)
    {
        services.AddKeyedSingleton(key, (sp, _) =>
        {
            var container = sp.GetRequiredService<BlobServiceClient>()
                .GetBlobContainerClient(containerName(Options(sp)));
            container.CreateIfNotExists();
            return container;
        });
        return services;
    }

    private static StorageOptions Options(IServiceProvider sp) =>
        sp.GetRequiredService<IOptions<StorageOptions>>().Value;

    private static TableClient CreateTable(IServiceProvider sp, string name)
    {
        var table = sp.GetRequiredService<TableServiceClient>().GetTableClient(name);
        table.CreateIfNotExists();
        return table;
    }

    private static QueueClient CreateQueue(IServiceProvider sp, string name)
    {
        var queue = sp.GetRequiredService<QueueServiceClient>().GetQueueClient(name);
        queue.CreateIfNotExists();
        return queue;
    }
}
