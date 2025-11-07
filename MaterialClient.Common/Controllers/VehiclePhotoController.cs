using Microsoft.AspNetCore.Mvc;
using MaterialClient.Common.Services.Hardware;

namespace MaterialClient.Common.Controllers;

/// <summary>
/// Vehicle photo API controller for hardware testing
/// </summary>
[ApiController]
[Route("api/hardware/vehicle-photo")]
public class VehiclePhotoController : ControllerBase
{
    private readonly IVehiclePhotoService _vehiclePhotoService;

    public VehiclePhotoController(IVehiclePhotoService vehiclePhotoService)
    {
        _vehiclePhotoService = vehiclePhotoService;
    }

    /// <summary>
    /// Capture vehicle photos (returns 4 test photo paths)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetVehiclePhotos()
    {
        try
        {
            var photoPaths = await _vehiclePhotoService.CaptureVehiclePhotosAsync();
            return Ok(new { photoPaths });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

