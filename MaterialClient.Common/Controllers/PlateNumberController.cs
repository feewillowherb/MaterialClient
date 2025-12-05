using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services;
using Microsoft.Extensions.Logging;

namespace MaterialClient.Common.Controllers;

/// <summary>
/// Plate number capture API controller for hardware testing
/// </summary>
[ApiController]
[Route("api/hardware/plate-number")]
[AutoConstructor]
public class PlateNumberController : ControllerBase
{
    private readonly IPlateNumberCaptureService _plateNumberCaptureService;
    private readonly IAttendedWeighingService _attendedWeighingService;
    private readonly ILogger<PlateNumberController> _logger;

    [HttpPost]
    public async Task<IActionResult> CallDeviceMessage(dynamic input)
    {
        var result = new ResultInfo<object>();
        try
        {
            var data = input;
            //解析数据
            int channel = data.AlarmInfoPlate.channel;
            string deviceName = data.AlarmInfoPlate.deviceName;
            string ipAddress = data.AlarmInfoPlate.ipaddr;
            var plateResult = data.AlarmInfoPlate.result.PlateResult;
            string license = plateResult.license;

            // 将车牌识别结果传递给 AttendedWeighingService
            if (!string.IsNullOrWhiteSpace(license))
            {
                _attendedWeighingService.OnPlateNumberRecognized(license);
            }

            result.Success = true;
            result.Msg = "完成";
        }
        catch (Exception e)
        {
            result.Success = false;
            result.Msg = e.Message.ToString();
            _logger.LogError(e.StackTrace);
        }


        return Ok(result);
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

            return Task.FromResult<IActionResult>(StatusCode(500,
                new { error = "Service implementation not available" }));
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

public class ResultInfo<T>
{
    public int Result { get; set; } //表示接口是否调通，1成功，0失败，通常只要设备服务器能响应，该值均为1
    public bool Success { get; set; } //此次操作是否成功，成功为true，失败为false
    public T? Data { get; set; } //接口返回的业务数据，类型可为数值、字符串或集合等
    public string? Msg { get; set; } //接口返回的信息，通常是错误类型码的原因信息
}