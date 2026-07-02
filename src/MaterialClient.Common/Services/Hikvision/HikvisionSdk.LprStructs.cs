// SDK: CH-HCNetSDKV6.1.9.48_build20230410_win64
// Source: Fdsoft.Weight.GovClient/BLL/CHCNetSDK.cs (AlarmCSharpDemo, verified on iDS-TCM204-E)
//          Demo示例/12-交通产品/TrafficDemo/TrafficDemo/CHCNetSDK.cs (equivalent layout)
// Do NOT hand-edit field order or types; must match HCNetSDK.h for bundled HCNetSDK.dll.

using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Hikvision;

internal static partial class HikvisionSdk
{
    internal const int HikItsPictureTypeScene = 1;

    internal const int MaxLicenseLen = 16;
    internal const int MaxCategoryLen = 8;
    internal const int SerialNoLen = 48;
    internal const int NameLen = 32;
    internal const int MacAddrLen = 6;

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_TIME_V30
    {
        public ushort wYear;
        public byte byMonth;
        public byte byDay;
        public byte byHour;
        public byte byMinute;
        public byte bySecond;
        public byte byRes;
        public ushort wMilliSec;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes1;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct NET_DVR_ALARMER
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

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SerialNoLen, ArraySubType = UnmanagedType.I1)]
        public byte[] sSerialNumber;

        public uint dwDeviceVersion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NameLen, ArraySubType = UnmanagedType.I1)]
        public byte[] sDeviceName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MacAddrLen, ArraySubType = UnmanagedType.I1)]
        public byte[] byMacAddr;

        public ushort wLinkPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
        public byte[] sDeviceIP;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.I1)]
        public byte[] sSocketIP;

        public byte byIpProtocol;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_VCA_RECT
    {
        public float fX;
        public float fY;
        public float fWidth;
        public float fHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_PLATE_INFO
    {
        public byte byPlateType;
        public byte byColor;
        public byte byBright;
        public byte byLicenseLen;
        public byte byEntireBelieve;
        public byte byRegion;
        public byte byCountry;
        public byte byArea;
        public byte byPlateSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxCategoryLen, ArraySubType = UnmanagedType.I1)]
        public byte[] sPlateCategory;

        public uint dwXmlLen;
        public IntPtr pXmlBuf;
        public NET_VCA_RECT struPlateRect;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxLicenseLen, ArraySubType = UnmanagedType.I1)]
        public byte[] sLicense;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxLicenseLen, ArraySubType = UnmanagedType.I1)]
        public byte[] byBelieve;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_VEHICLE_INFO
    {
        public uint dwIndex;
        public byte byVehicleType;
        public byte byColorDepth;
        public byte byColor;
        public byte byRadarState;
        public ushort wSpeed;
        public ushort wLength;
        public byte byIllegalType;
        public byte byVehicleLogoRecog;
        public byte byVehicleSubLogoRecog;
        public byte byVehicleModel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.I1)]
        public byte[] byCustomInfo;

        public ushort wVehicleLogoRecog;
        public byte byIsParking;
        public byte byRes;
        public uint dwParkingTime;
        public byte byBelieve;
        public byte byCurrentWorkerNumber;
        public byte byCurrentGoodsLoadingRate;
        public byte byDoorsStatus;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_PLATE_RESULT
    {
        public uint dwSize;
        public byte byResultType;
        public byte byChanIndex;
        public ushort wAlarmRecordID;
        public uint dwRelativeTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32, ArraySubType = UnmanagedType.I1)]
        public byte[] byAbsTime;

        public uint dwPicLen;
        public uint dwPicPlateLen;
        public uint dwVideoLen;
        public byte byTrafficLight;
        public byte byPicNum;
        public byte byDriveChan;
        public byte byVehicleType;
        public uint dwBinPicLen;
        public uint dwCarPicLen;
        public uint dwFarCarPicLen;
        public IntPtr pBuffer3;
        public IntPtr pBuffer4;
        public IntPtr pBuffer5;
        public byte byRelaLaneDirectionType;
        public byte byCarDirectionType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.I1)]
        public byte[] byRes3;

        public NET_DVR_PLATE_INFO struPlateInfo;
        public NET_DVR_VEHICLE_INFO struVehicleInfo;
        public IntPtr pBuffer1;
        public IntPtr pBuffer2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_ITS_PICTURE_INFO
    {
        public uint dwDataLen;
        public byte byType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] byRes1;

        public uint dwRedLightTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] byAbsTime;

        public NET_VCA_RECT struPlateRect;
        public NET_VCA_RECT struPlateRecgRect;
        public IntPtr pBuffer;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] byRes2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NET_ITS_PLATE_RESULT
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

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.I1)]
        public byte[] byIllegalSubType;

        public byte byPostPicNo;
        public byte byChanIndex;
        public ushort wSpeedLimit;
        public byte byChanIndexEx;
        public byte byRes2;
        public NET_DVR_PLATE_INFO struPlateInfo;
        public NET_DVR_VEHICLE_INFO struVehicleInfo;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48, ArraySubType = UnmanagedType.I1)]
        public byte[] byMonitoringSiteID;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48, ArraySubType = UnmanagedType.I1)]
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

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.Struct)]
        public NET_ITS_PICTURE_INFO[] struPicInfo;
    }
}
