using WeatherAPI.DTOs;
using WeatherAPI.Messaging;
using WeatherAPI.Models;
using WeatherAPI.Repositories.Blobs;
using WeatherAPI.Repositories.Tables;

namespace WeatherAPI.Services;

public class ImageService: IImageService
{
    private IQueuePublisher  _queuePublisher;
    private ICreationProcessRepository  _creationProcessRepository;
    private IGeneratedImageRepository  _generatedImageRepository;
    private IImageBlobRepository _imageBlobRepository;
    public ImageService(IQueuePublisher queuePublisher, ICreationProcessRepository creationProcessRepository, IGeneratedImageRepository generatedImageRepository, IImageBlobRepository imageBlobRepository)
    {
        _queuePublisher = queuePublisher;
        _creationProcessRepository = creationProcessRepository;
        _generatedImageRepository = generatedImageRepository;
        _imageBlobRepository = imageBlobRepository;
    }
    public async Task<GenerationResponse> Generate()
    {
        string messageId = await _queuePublisher.PublishAsync("Generate message");
        CreationProcess creationProcess = _creationProcessRepository.Generate(messageId);
        return CreationModelToGenerationResponse(creationProcess);
    }

    private GenerationResponse CreationModelToGenerationResponse(CreationProcess creationProcess)
    {
        return new GenerationResponse(creationProcess.RowKey, creationProcess.Status, creationProcess.ETag, creationProcess.Timestamp);
    }

    public async Task<FullGenerationResponse> GetGeneration(string id)
    {
        CreationProcess creationProcess = await _creationProcessRepository.GetCreationProcess(id);
        List<GeneratedImage> generatedImages = await _generatedImageRepository.GetGeneratedImages(id);
        
        await Task.WhenAll(generatedImages.Select(async img =>
        {
            img.SasUrl = await _imageBlobRepository.GetImageSasUrlAsync(img.ImageUrl);
        }));
        
        List<GeneratedImageResponse> generatedImageResponses = generatedImages.Select(img => new GeneratedImageResponse(img.Status, img.SasUrl, img.ETag, img.StationName)).ToList();

        return new FullGenerationResponse(creationProcess.Status, creationProcess.ETag, creationProcess.Timestamp, generatedImageResponses);
    }
    
}