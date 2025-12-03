using Microsoft.AspNetCore.Mvc;
using MaterialClient.Common.Services.Hardware;

namespace MaterialClient.Common.Controllers;

/// <summary>
/// Plate number capture API controller for hardware testing
/// </summary>
[ApiController]
[Route("api/hardware/plate-number")]
public class PlateNumberController : ControllerBase
{
    private readonly IPlateNumberCaptureService _plateNumberCaptureService;

    public PlateNumberController(IPlateNumberCaptureService plateNumberCaptureService)
    {
        _plateNumberCaptureService = plateNumberCaptureService;
    }




    /// <summary>
    /// Get current captured plate number (for testing)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPlateNumber()
    {
        try
        {
            var plateNumber = await _plateNumberCaptureService.CapturePlateNumberAsync();
            return Ok(new { plateNumber });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set plate number test value
    /// </summary>
    [HttpPost]
    public Task<IActionResult> SetPlateNumber([FromBody] SetPlateNumberRequest request)
    {
        try
        {
            // Cast to implementation to access SetPlateNumber method
            if (_plateNumberCaptureService is PlateNumberCaptureService service)
            {
                service.SetPlateNumber(request.PlateNumber);
                return Task.FromResult<IActionResult>(Ok(new { success = true, message = "Plate number updated" }));
            }

            return Task.FromResult<IActionResult>(StatusCode(500, new { error = "Service implementation not available" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult<IActionResult>(StatusCode(500, new { error = ex.Message }));
        }
    }

    public class SetPlateNumberRequest
    {
        public string? PlateNumber { get; set; }
    }
}