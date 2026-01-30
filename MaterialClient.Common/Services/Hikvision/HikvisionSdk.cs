using System.Runtime.InteropServices;

namespace MaterialClient.Common.Services.Hikvision;

/// <summary>
///     海康威视车牌识别 SDK P/Invoke 声明
///     包含 LPR 功能所需的核心 API、回调和结构体定义
/// </summary>
internal static class HikvisionSdk
{
    #region 常量定义

    /// <summary>
    ///     车牌识别结果上传 (COMM_UPLOAD_PLATE_RESULT)
    /// </summary>
    public const int COMM_UPLOAD_PLATE_RESULT = 0x2800;

    /// <summary>
    ///     ITS 车牌识别结果上传 (COMM_ITS_PLATE_RESULT)
    /// </summary>
    public const int COMM_ITS_PLATE_RESULT = 0x3050;

    #endregion

    #region 核心API

    /// <summary>
    ///     SDK 初始化
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern bool NET_DVR_Init();

    /// <summary>
    ///     SDK 清理，释放资源
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern void NET_DVR_Cleanup();

    /// <summary>
    ///     设备登录
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern int NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO pLoginInfo,
        ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

    /// <summary>
    ///     设备登出
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern bool NET_DVR_Logout(int lUserID);

    /// <summary>
    ///     启动监听
    /// </summary>
    /// <param name="sLocalIP">本地监听 IP 地址</param>
    /// <param name="wLocalPort">本地监听端口</param>
    /// <param name="fMessageCallBack">消息回调函数</param>
    /// <param name="pUser">用户数据</param>
    /// <returns>=-1 表示失败，其他值表示监听句柄</returns>
    [DllImport("HCNetSDK.dll")]
    public static extern int NET_DVR_StartListen_V30(string sLocalIP, ushort wLocalPort,
        MSGCallBack fMessageCallBack, IntPtr pUser);

    /// <summary>
    ///     停止监听
    /// </summary>
    /// <param name="lListenHandle">监听句柄（由 NET_DVR_StartListen_V30 返回）</param>
    /// <returns>true 表示成功，false 表示失败</returns>
    [DllImport("HCNetSDK.dll")]
    public static extern bool NET_DVR_StopListen_V30(int lListenHandle);

    /// <summary>
    ///     连续抓拍
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern bool NET_DVR_ContinuousShoot(int lUserID, int lChannel, uint dwShootInterval,
        out uint lpJpegSize, byte[] pJpegBuffer, uint dwBufSize);

    /// <summary>
    ///     获取最后一次错误码
    /// </summary>
    [DllImport("HCNetSDK.dll")]
    public static extern uint NET_DVR_GetLastError();

    #endregion

    #region 回调委托

    /// <summary>
    ///     消息回调委托
    /// </summary>
    /// <param name="lCommand">消息命令</param>
    /// <param name="pAlarmer">报警器信息</param>
    /// <param name="pAlarmInfo">报警信息</param>
    /// <param name="dwBufLen">信息长度</param>
    /// <param name="pUser">用户数据</param>
    public delegate void MSGCallBack(int lCommand, IntPtr pAlarmer, IntPtr pAlarmInfo, uint dwBufLen,
        IntPtr pUser);

    #endregion

    #region 结构体定义

    /// <summary>
    ///     设备登录信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_USER_LOGIN_INFO
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)]
        public byte[] sDeviceAddress;

        public byte byUseTransport;
        public ushort wPort;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] sUserName;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] sPassword;

        public IntPtr cbLoginResult;
        public IntPtr pUser;
        public int bUseAsynLogin;
        public byte byProxyType;
        public byte byUseUTCTime;
        public byte byLoginMode;
        public byte byHttps;
        public int iProxyID;
        public byte byVerifyMode;
        public byte byRes3;
        public ushort wTaskNo;
        public int byRes4;
        public int byRes5;
    }

    /// <summary>
    ///     设备信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_DEVICEINFO_V30
    {
        public int dwSize;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] sSerialNumber;

        public int byAlarmInPortNum;
        public int byAlarmOutPortNum;
        public int byDiskNum;
        public int byDVRType;
        public int byChanNum;
        public int byStartChan;
        public int byAudioChanNum;
        public int byIPChanNum;
    }

    /// <summary>
    ///     设备信息 V40
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_DEVICEINFO_V40
    {
        public NET_DVR_DEVICEINFO_V30 struDeviceV30;
        public int bySupportLock;
        public int byRetryLoginTime;
        public int byPasswordLevel;
        public int byProxyType;
        public int dwSurplusLockTime;
        public int byCharEncodeType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] byRes2;
    }

    /// <summary>
    ///     抓拍配置
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_SNAPCFG
    {
        public int dwSnapInterval;
        public int bySnapCount;
        public int byRes;
    }

    /// <summary>
    ///     报警器信息（用于识别设备）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_ALARMER
    {
        public int dwAlarmType;
        public int byAlarmOutputNumber;
        public int byAlarmInfoChannel;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)]
        public byte[] sDeviceIP;

        public byte byDevicePort;
        public byte byAlarmInputIndex;
        public byte byChannel;
        public byte byRes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] sSerialNumber;

        public uint dwDeviceVersion;
        public int byAlarmChannel;
        public int byRes1;
    }

    /// <summary>
    ///     车牌识别结果
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_PLATE_RESULT
    {
        public int dwResult;
        public int dwPicLen;
        public IntPtr pBuffer;
        public uint dwRelativeTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] sLicense;

        public int byPicNum;
        public int byLaneNo;
        public int byDriveChan;
        public int byVehicleType;
        public int byColor;
        public int byVehicleSpeed;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] byRes;

        public int dwSysRefTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] sDirection;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] byRes1;

        public NET_DVR_PLATE_INFO_EX struPlateInfo;
    }

    /// <summary>
    ///     车牌信息扩展
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_PLATE_INFO_EX
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] byRegion;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] byClass;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] byColor;

        public int byBelief;
        public int byColorViolet;
        public int byPlateType;
        public int byConfidence;
        public int byBrightness;
        public int byContrast;
        public int byDirection;
        public int byRes;
    }

    /// <summary>
    ///     ITS 车牌识别结果
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_ITS_PLATE_RESULT
    {
        public int dwResultNum;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public NET_ITS_PLATE_INFO[] struPlateInfo;

        public int dwRelativeTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public byte[] byRes;
    }

    /// <summary>
    ///     ITS 车牌信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_ITS_PLATE_INFO
    {
        public int dwPicLen;
        public IntPtr pBuffer;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] sLicense;

        public int byLaneNo;
        public int byVehicleType;
        public int byColor;
        public int byVehicleSpeed;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] byRes1;

        public uint dwSysRefTime;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public byte[] sDirection;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] byRes2;

        public NET_DVR_PLATE_INFO_EX struPlateInfoEx;
    }

    /// <summary>
    ///     JPEG 图片参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NET_DVR_JPEGPARA
    {
        public ushort wPicSize;
        public ushort wPicQuality;
    }

    #endregion
}
