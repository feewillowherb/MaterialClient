using Microsoft.AspNetCore.Mvc;
using MaterialClient.Common.Services.Hardware;

namespace MaterialClient.Common.Controllers;

/// <summary>
/// Bill photo API controller for hardware testing
/// </summary>
[ApiController]
[Route("api/hardware/bill-photo")]
public class BillPhotoController : ControllerBase
{
    private readonly IBillPhotoService _billPhotoService;

    public BillPhotoController(IBillPhotoService billPhotoService)
    {
        _billPhotoService = billPhotoService;
    }

    /// <summary>
    /// Capture bill photo (returns 1 test photo path)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBillPhoto()
    {
        try
        {
            var photoPath = await _billPhotoService.CaptureBillPhotoAsync();
            return Ok(new { photoPath });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

