using Microsoft.AspNetCore.Mvc;
using WeatherAPI.DTOs;
using WeatherAPI.Services;

namespace WeatherAPI.Controllers;

[ApiController]
[Route("api/images")]
public class ImageController(IImageService imageService) : ControllerBase
{
    /// <summary>Starts a new image generation process. Returns 202 with the process id and a status link.</summary>
    [HttpPost("generate")]
    [ProducesResponseType<GenerationResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Generate(CancellationToken cancellationToken)
    {
        var response = await imageService.StartGenerationAsync(
            id => Url.Action(nameof(Get), null, new { id }, Request.Scheme) ?? $"/api/images/{id}",
            cancellationToken);

        return Accepted(response.StatusUrl, response);
    }

    /// <summary>Returns the status of a process and links to every finished image.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<FullGenerationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var generation = await imageService.GetGenerationAsync(id, cancellationToken);
        return generation is null ? NotFound() : Ok(generation);
    }
}
