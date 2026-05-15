using MaterialClient.Demo.Models;

namespace MaterialClient.Demo.ViewModels;

public static class WeighingSystemDataGenerator
{
    public static List<WeighingRecord> GetWeighingRecords()
    {
        return
        [
            new() { LicensePlate = "浙A06L07", WeighingTime = "05-06 16:30", Weight = 9.81, Status = "正常" },
            new() { LicensePlate = "浙A98J22", WeighingTime = "05-06 16:27", Weight = 8.20, Status = "正常" },
            new() { LicensePlate = "浙A96H93", WeighingTime = "05-06 16:16", Weight = 11.03, Status = "正常" },
            new() { LicensePlate = "浙A62J79", WeighingTime = "05-06 15:43", Weight = 7.55, Status = "异常" },
            new() { LicensePlate = "浙A02G55", WeighingTime = "05-06 15:13", Weight = 10.40, Status = "正常" },
            new() { LicensePlate = "浙A06L07", WeighingTime = "05-06 13:47", Weight = 6.78, Status = "异常" },
        ];
    }

    public static List<DeviceStatus> GetDeviceStatuses()
    {
        return
        [
            new() { DeviceName = "地磅设备", IsOnline = true },
            new() { DeviceName = "摄像头", IsOnline = true },
            new() { DeviceName = "车牌识别", IsOnline = false },
        ];
    }
}
