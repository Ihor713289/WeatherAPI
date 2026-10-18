using Microsoft.AspNetCore.Mvc;
using WeatherAPI.Services;

namespace WeatherAPI.Controllers;

[ApiController]
[Route("api/images")]
public class ImageController : ControllerBase
{
    private IImageService _imageService;
    public ImageController(IImageService imageService)
    {
        _imageService = imageService;
    }
    
    [HttpPost("generate")]
    public async Task<IActionResult> Generate()
    {
        return Ok(await _imageService.Generate());
    }
    
    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        return Ok();
    }
}