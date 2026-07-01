using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康 ITS 交通抓拍相关结构体（与 HCNetSDK.h 对齐，Pack=1）。
/// </summary>
internal static partial class HikvisionItsStructs
{
    internal const int MaxLicenseLen = 16;
    internal const int SerialNoLen = 48;
    internal const int NameLen = 129;
    internal const int MacAddrLen = 6;
    internal const int DeviceIpLen = 128;
    internal const int ItsPictureCount = 6;

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct NET_DVR_ALARMER
    {
        public byte byUserIDValid;
        public byte bySerialValid;
        public byte byVersionValid;
        public byte byDeviceNameValid;
        public byte byMacAddrValid;
        public byte byLinkPortValid;
        public byte byDeviceIPValid;
        public byte bySocketIPValid;
        public int lUserID;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SerialNoLen)]
        public byte[] sSerialNumber;

        public uint dwDeviceVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NameLen)]
        public byte[] sDeviceName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MacAddrLen)]
        public byte[] byMacAddr;

        public ushort wLinkPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = DeviceIpLen)]
        public byte[] sDeviceIP;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = DeviceIpLen)]
        public byte[] sSocketIP;

        public byte byIpProtocol;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NET_VCA_RECT
    {
        public float fX;
        public float fY;
        public float fWidth;
        public float fHeight;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct NET_DVR_PLATE_INFO
    {
        public byte byPlateType;
        public byte byColor;
        public byte byBright;
        public byte byLicenseLen;
        public byte byEntireBelieve;
        public byte byRegion;
        public byte byCountry;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
        public byte[] byRes;

        public uint dwXmlLen;
        public IntPtr pXmlBuf;
        public NET_VCA_RECT struPlateRect;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxLicenseLen)]
        public byte[] sLicense;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxLicenseLen)]
        public byte[] byBelieve;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NET_DVR_VEHICLE_INFO
    {
        public uint dwIndex;
        public byte byVehicleType;
        public byte byColorDepth;
        public byte byColor;
        public byte byRaderState;
        public ushort wSpeed;
        public ushort wLength;
        public byte byIllegalType;
        public byte byVehicleLogoRecog;
        public byte byVehicleSubLogoRecog;
        public byte byVehicleModel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] byCustomInfo;

        public ushort wVehicleLogoRecog;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
        public byte[] byRes3;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NET_DVR_TIME_V30
    {
        public ushort wYear;
        public byte byMonth;
        public byte byDay;
        public byte byHour;
        public byte byMinute;
        public byte bySecond;
        public byte byRes;
        public ushort wMilliSec;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] byRes1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NET_ITS_PICTURE_INFO
    {
        public uint dwDataLen;
        public byte byType;
        public byte byDataType;
        public byte byCloseUpType;
        public byte byPicRecogMode;
        public uint dwRedLightTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] byAbsTime;

        public NET_VCA_RECT struPlateRect;
        public NET_VCA_RECT struPlateRecgRect;
        public IntPtr pBuffer;
        public uint dwUTCTime;
        public byte byCompatibleAblity;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct NET_ITS_PLATE_RESULT
    {
        public uint dwSize;
        public uint dwMatchNo;
        public byte byGroupNum;
        public byte byPicNo;
        public byte bySecondCam;
        public byte byFeaturePicNo;
        public byte byDriveChan;
        public byte byVehicleType;
        public byte byDetSceneID;
        public byte byVehicleAttribute;
        public ushort wIllegalType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] byIllegalSubType;

        public byte byPostPicNo;
        public byte byChanIndex;
        public ushort wSpeedLimit;
        public byte byChanIndexEx;
        public byte byRes2;
        public NET_DVR_PLATE_INFO struPlateInfo;
        public NET_DVR_VEHICLE_INFO struVehicleInfo;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] byMonitoringSiteID;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] byDeviceID;

        public byte byDir;
        public byte byDetectType;
        public byte byRelaLaneDirectionType;
        public byte byCarDirectionType;
        public uint dwCustomIllegalType;
        public IntPtr pIllegalInfoBuf;
        public byte byIllegalFromatType;
        public byte byPendant;
        public byte byDataAnalysis;
        public byte byYellowLabelCar;
        public byte byDangerousVehicles;
        public byte byPilotSafebelt;
        public byte byCopilotSafebelt;
        public byte byPilotSunVisor;
        public byte byCopilotSunVisor;
        public byte byPilotCall;
        public byte byBarrierGateCtrlType;
        public byte byAlarmDataType;
        public NET_DVR_TIME_V30 struSnapFirstPicTime;
        public uint dwIllegalTime;
        public uint dwPicNum;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = ItsPictureCount)]
        public NET_ITS_PICTURE_INFO[] struPicInfo;
    }

    internal static string ReadAscii(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;

        return length == 0 ? string.Empty : Encoding.ASCII.GetString(bytes, 0, length);
    }

    internal static string ResolveDeviceIp(NET_DVR_ALARMER alarmer)
    {
        if (alarmer.byDeviceIPValid == 1)
        {
            var ip = ReadAscii(alarmer.sDeviceIP);
            if (!string.IsNullOrWhiteSpace(ip))
                return ip.Trim();
        }

        var deviceName = ReadAscii(alarmer.sDeviceName);
        var match = DeviceIpRegex().Match(deviceName);
        if (match.Success)
            return match.Value;

        match = DeviceIpRegex().Match(ReadAscii(alarmer.sDeviceIP));
        return match.Success ? match.Value : ReadAscii(alarmer.sDeviceIP).Trim();
    }

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b")]
    private static partial Regex DeviceIpRegex();
}
