using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MaterialClient.Common.Services.Hardware;

namespace MaterialClient.Common.Controllers;

/// <summary>
/// Truck scale weight API controller for hardware testing
/// </summary>
[ApiController]
[Route("api/hardware/truck-scale")]
public class TruckScaleWeightController : ControllerBase
{
    private readonly ITruckScaleWeightService _truckScaleWeightService;

    public TruckScaleWeightController(ITruckScaleWeightService truckScaleWeightService)
    {
        _truckScaleWeightService = truckScaleWeightService;
    }

    /// <summary>
    /// Get current truck scale weight value (for testing)
    /// </summary>
    [HttpGet("weight")]
    public async Task<IActionResult> GetWeight()
    {
        try
        {
            var weight = await _truckScaleWeightService.GetCurrentWeightAsync();
            return Ok(new { weight });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set truck scale weight test value
    /// </summary>
    [HttpPost("weight")]
    public Task<IActionResult> SetWeight([FromBody] SetWeightRequest request)
    {
        try
        {
            if (request == null || request.Weight < 0)
            {
                return Task.FromResult<IActionResult>(BadRequest(new { error = "Invalid weight value" }));
            }

            // Cast to implementation to access SetWeight method
            if (_truckScaleWeightService is TruckScaleWeightService service)
            {
                service.SetWeight(request.Weight);
                return Task.FromResult<IActionResult>(Ok(new { success = true, message = "Weight value updated" }));
            }

            return Task.FromResult<IActionResult>(StatusCode(500, new { error = "Service implementation not available" }));
        }
        catch (Exception ex)
        {
            return Task.FromResult<IActionResult>(StatusCode(500, new { error = ex.Message }));
        }
    }

    public class SetWeightRequest
    {
        public decimal Weight { get; set; }
    }
}

