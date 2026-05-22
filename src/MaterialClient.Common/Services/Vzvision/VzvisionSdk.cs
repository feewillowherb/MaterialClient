using System;
using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Vzvision;

/// <summary>
/// Vz 车牌识别（LPR）SDK P/Invoke 封装（仅接口声明，不包含业务接入）。
/// </summary>
internal static class VzvisionSdk
{
    private const string DllName = "VzLPRSDK.dll";

    #region 全局初始化/释放

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_Setup();

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void VzLPRClient_Cleanup();

    #endregion

    #region 设备生命周期

    /// <summary>
    /// 打开设备：返回设备句柄（Win32 下为 int）。
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int VzLPRClient_Open(
        [MarshalAs(UnmanagedType.LPStr)] string pStrIP,
        ushort wPort,
        [MarshalAs(UnmanagedType.LPStr)] string pStrUserName,
        [MarshalAs(UnmanagedType.LPStr)] string pStrPassword);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_Close(int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int VzLPRClient_CloseByIP([MarshalAs(UnmanagedType.LPStr)] string pStrIP);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_IsConnected(int handle, out byte pStatus);

    #endregion

    #region 车牌识别：视频播放与回调

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_StartRealPlay(int handle, IntPtr hWnd);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_StopRealPlay(int nPlayHandle);

    public enum VZ_LPRC_RESULT_TYPE : int
    {
        VZ_LPRC_RESULT_REALTIME = 0, // 实时识别结果
        VZ_LPRC_RESULT_STABLE = 1, // 稳定识别结果
        VZ_LPRC_RESULT_FORCE_TRIGGER = 2, // 软件触发（ForceTrigger）
        VZ_LPRC_RESULT_IO_TRIGGER = 3, // IO 触发结果
        VZ_LPRC_RESULT_VLOOP_TRIGGER = 4, // 虚拟线圈触发结果
        VZ_LPRC_RESULT_MULTI_TRIGGER = 5, // 多触发
        VZ_LPRC_RESULT_RETENTION_TRIGGER = 64, // 滞留
        VZ_LPRC_RESULT_RELIEVE_TRIGGER = 65, // 滞留解除
        VZ_LPRC_RESULT_TYPE_NUM = 66 // 结果种类个数
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TH_RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VzBDTime
    {
        public byte bdt_sec;
        public byte bdt_min;
        public byte bdt_hour;
        public byte bdt_mday;
        public byte bdt_mon;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] res1;

        public uint bdt_year;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I1)]
        public byte[] res2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VZ_TIMEVAL
    {
        public uint uTVSec;
        public uint uTVUSec;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CarBrand
    {
        public byte brand;
        public byte type;
        public ushort year;
    }

    private const int TH_LP_LEN = 16;

    /// <summary>
    /// 车牌识别结果信息（与 SDK 头文件布局保持一致）。
    /// 注意：SDK 返回的是该结构体数组的指针（pResult），业务侧需自行 Marshal。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TH_PlateResult
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = TH_LP_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] license;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.I1)]
        public byte[] color;

        public int nColor;
        public int nType;
        public int nConfidence;
        public int nBright;
        public int nDirection;

        public TH_RECT rcLocation;
        public int nTime;
        public VZ_TIMEVAL tvPTS;
        public uint uBitsTrigType;
        public byte nCarBright;
        public byte nCarColor;
        public byte reserved0; // 对齐占位（C char）

        public uint uId;
        public VzBDTime struBDTime;

        public byte nIsEncrypt;
        public byte nPlateTrueWidth;
        public byte nPlateDistance;
        public byte nIsFakePlate;

        public TH_RECT car_location;
        public CarBrand car_brand;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = TH_LP_LEN, ArraySubType = UnmanagedType.I1)]
        public byte[] license1;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I1)]
        public byte[] featureCode;

        public byte nPlateTypeExtInfo;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3, ArraySubType = UnmanagedType.I1)]
        public byte[] reservedC1;

        public uint triggerTimeMS;
        public byte rule_id;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15, ArraySubType = UnmanagedType.I1)]
        public byte[] reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VZ_LPRC_IMAGE_INFO
    {
        public uint uWidth;
        public uint uHeight;
        public uint uPitch;
        public uint uPixFmt;
        public IntPtr pBuffer;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int VZLPRC_PLATE_INFO_CALLBACK(
        int handle,
        IntPtr pUserData,
        IntPtr pResult,
        uint uNumPlates,
        VZ_LPRC_RESULT_TYPE eResultType,
        IntPtr pImgFull,
        IntPtr pImgPlateClip);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_SetPlateInfoCallBack(
        int handle,
        VZLPRC_PLATE_INFO_CALLBACK func,
        IntPtr pUserData,
        int bEnableImage);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_ForceTrigger(int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_ForceTriggerEx(int handle);

    #endregion

    #region IO 控制（新增）

    /// <summary>
    /// 获取 IO 输出状态：pOutput=0 开路，pOutput=1 闭路
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_GetIOOutput(int handle, uint uChnId, out int pOutput);

    /// <summary>
    /// 设置 IO 输出状态：nOutput=0 开路，nOutput=1 闭路
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_SetIOOutput(int handle, uint uChnId, int nOutput);

    /// <summary>
    /// IO 输出并自动复位：延时 nDuration（毫秒，SDK 约束[500,5000]）
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_SetIOOutputAuto(int handle, uint uChnId, int nDuration);

    /// <summary>
    /// IO 输出并自动复位（带响应/等待回写开闸成功结果）。
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_SetIOOutputAutoResp(int handle, uint uChnId, int nDuration);

    /// <summary>
    /// GPIO 输入监听（可选，便于后续读取 IO 输入状态/触发）。
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void VZLPRC_GPIO_RECV_CALLBACK(int handle, int nGPIOId, int nVal, IntPtr pUserData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_SetGPIORecvCallBack(int handle, VZLPRC_GPIO_RECV_CALLBACK func,
        IntPtr pUserData);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int VzLPRClient_GetGPIOValue(int handle, int gpioIn, out int value);

    #endregion
}

