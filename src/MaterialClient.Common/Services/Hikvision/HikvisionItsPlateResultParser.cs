using System.Runtime.InteropServices;
using System.Text;
using MaterialClient.Common.Utils;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     COMM_ITS_PLATE_RESULT (NET_ITS_PLATE_RESULT) 结构体解析与诊断输出
/// </summary>
internal static class HikvisionItsPlateResultParser
{
    /// <summary>
    ///     从 SDK 回调指针解析 ITS 车牌识别结果
    /// </summary>
    public static HikvisionSdk.NET_ITS_PLATE_RESULT Parse(IntPtr pAlarmInfo)
    {
        return Marshal.PtrToStructure<HikvisionSdk.NET_ITS_PLATE_RESULT>(pAlarmInfo);
    }

    /// <summary>
    ///     生成与设备回调一致的可读诊断文本
    /// </summary>
    public static string Format(HikvisionSdk.NET_ITS_PLATE_RESULT result)
    {
        var plate = result.struPlateInfo;
        var vehicle = result.struVehicleInfo;
        var rect = plate.struPlateRect;
        var snapTime = FormatTime(result.struSnapFirstPicTime);

        var builder = new StringBuilder();
        builder.AppendLine("【COMM_ITS_PLATE_RESULT 车牌抓拍上报】");
        builder.AppendLine($"结构体长度：{result.dwSize}");
        builder.AppendLine($"车牌号(GBK)：{HikvisionEncodingHelper.GetString(plate.sLicense)}");
        builder.AppendLine($"车牌颜色：{DescribePlateColor(plate.byColor)}({plate.byColor})");
        builder.AppendLine($"车牌类型：{DescribePlateType(plate.byPlateType)}({plate.byPlateType})");
        builder.AppendLine($"识别置信度：{plate.byEntireBelieve}");
        builder.AppendLine($"车身颜色：{DescribeVehicleColor(vehicle.byColor)}({vehicle.byColor})");
        builder.AppendLine($"车牌坐标：左{(int)rect.fX} 上{(int)rect.fY} 右{(int)rect.fWidth} 下{(int)rect.fHeight}");
        builder.AppendLine($"车辆类型：{DescribeVehicleType(result.byVehicleType)}({result.byVehicleType})");
        builder.AppendLine($"车道号：{result.byDriveChan}");
        builder.AppendLine($"数据类型：{DescribeAlarmDataType(result.byAlarmDataType)}({result.byAlarmDataType})");
        builder.AppendLine($"抓拍时间：{snapTime}");
        builder.AppendLine($"违法时长：{result.dwIllegalTime}ms");
        builder.AppendLine($"抓拍图片总数：{result.dwPicNum}张");

        var picCount = Math.Min((int)result.dwPicNum, result.struPicInfo?.Length ?? 0);
        for (var i = 0; i < picCount; i++)
        {
            var pic = result.struPicInfo[i];
            var picTime = FormatPictureTime(pic.byAbsTime, result.struSnapFirstPicTime);
            builder.AppendLine(
                $"图片{i + 1}：类型={pic.byType}({DescribePictureType(pic.byType)})，图片大小={pic.dwDataLen}字节，抓拍时间{picTime}");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    ///     选取 LPR 附件图片：优先车牌特写，其次车辆图，最后首张有效图
    /// </summary>
    public static HikvisionSdk.NET_ITS_PICTURE_INFO? SelectLprPicture(HikvisionSdk.NET_ITS_PLATE_RESULT result)
    {
        if (result.struPicInfo == null || result.dwPicNum == 0)
            return null;

        var picCount = Math.Min((int)result.dwPicNum, result.struPicInfo.Length);
        HikvisionSdk.NET_ITS_PICTURE_INFO? fallback = null;

        for (var i = 0; i < picCount; i++)
        {
            var pic = result.struPicInfo[i];
            if (pic.dwDataLen == 0 || pic.pBuffer == IntPtr.Zero)
                continue;

            fallback ??= pic;

            if (pic.byType is 2 or 3)
                return pic;
        }

        for (var i = 0; i < picCount; i++)
        {
            var pic = result.struPicInfo[i];
            if (pic.dwDataLen == 0 || pic.pBuffer == IntPtr.Zero)
                continue;

            if (pic.byType == 1)
                return pic;
        }

        return fallback;
    }

    private static string FormatTime(HikvisionSdk.NET_DVR_TIME_V30 time)
    {
        return $"{time.wYear:D4}-{time.byMonth:D2}-{time.byDay:D2} {time.byHour:D2}:{time.byMinute:D2}:{time.bySecond:D2}";
    }

    private static string FormatPictureTime(byte[]? byAbsTime, HikvisionSdk.NET_DVR_TIME_V30 fallback)
    {
        if (byAbsTime != null && byAbsTime.Length >= 14)
        {
            var absTime = Encoding.ASCII.GetString(byAbsTime).TrimEnd('\0');
            if (absTime.Length >= 14 &&
                int.TryParse(absTime[..4], out var year) &&
                int.TryParse(absTime.Substring(4, 2), out var month) &&
                int.TryParse(absTime.Substring(6, 2), out var day) &&
                int.TryParse(absTime.Substring(8, 2), out var hour) &&
                int.TryParse(absTime.Substring(10, 2), out var minute) &&
                int.TryParse(absTime.Substring(12, 2), out var second))
            {
                return $"{year:D4}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}:{second:D2}";
            }
        }

        return FormatTime(fallback);
    }

    private static string DescribePlateColor(byte value) => value switch
    {
        0 => "蓝色",
        1 => "黄色",
        2 => "白色",
        3 => "黑色",
        4 => "绿色",
        _ => "未知"
    };

    private static string DescribePlateType(byte value) => value switch
    {
        0 => "普通蓝牌",
        _ => "其他"
    };

    private static string DescribeVehicleColor(byte value) => value switch
    {
        0 => "未知",
        1 => "白色",
        2 => "黑色",
        3 => "灰色",
        4 => "银色",
        5 => "银色",
        6 => "红色",
        7 => "蓝色",
        8 => "黄色",
        9 => "绿色",
        _ => "其他"
    };

    private static string DescribeVehicleType(byte value) => value switch
    {
        0 => "其他",
        1 => "小型轿车",
        2 => "大型车",
        _ => "其他"
    };

    private static string DescribeAlarmDataType(byte value) => value switch
    {
        0 => "实时过车",
        1 => "历史数据",
        _ => "未知"
    };

    private static string DescribePictureType(byte value) => value switch
    {
        0 => "车牌图",
        1 => "全景图",
        2 => "车牌特写",
        3 => "特写图",
        _ => "其他"
    };

    internal static string GetPlateColorDescription(byte value) => DescribePlateColor(value);

    internal static string GetVehicleColorDescription(byte value) => DescribeVehicleColor(value);

    internal static string GetVehicleTypeDescription(byte value) => DescribeVehicleType(value);
}
