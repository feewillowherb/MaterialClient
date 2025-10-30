using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MaterialClient.Common.Services.Hikvision;

public sealed class HikvisionService
{
	private readonly ConcurrentDictionary<string, int> deviceKeyToUserId = new();

	public static uint GetLastErrorCode() => NET_DVR.NET_DVR_GetLastError();

	public void AddOrUpdateDevice(HikvisionDeviceConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		var key = BuildDeviceKey(config);
		deviceKeyToUserId.AddOrUpdate(key, _ => -1, (_, __) => -1);
	}

	public bool IsOnline(HikvisionDeviceConfig config)
	{
		ArgumentNullException.ThrowIfNull(config);
		EnsureInitialized();
		var login = TryLogin(config, out var userId);
		if (login)
		{
			NET_DVR.NET_DVR_Logout(userId);
		}
		return login;
	}

	public bool CaptureJpeg(HikvisionDeviceConfig config, int channel, string saveFullPath, int quality = 90)
	{
		ArgumentNullException.ThrowIfNull(config);
		if (string.IsNullOrWhiteSpace(saveFullPath)) throw new ArgumentException("saveFullPath is required", nameof(saveFullPath));
		EnsureInitialized();

		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saveFullPath))!);

		if (!TryLogin(config, out var userId))
		{
			return false;
		}

		try
		{
			NET_DVR.NET_DVR_JPEGPARA para = new NET_DVR.NET_DVR_JPEGPARA
			{
				wPicQuality = (ushort)Math.Clamp(quality, 0, 100),
				wPicSize = 0xFF, // use device default
			};
			var pathBytes = Encoding.ASCII.GetBytes(saveFullPath + "\0");
			bool ok = NET_DVR.NET_DVR_CaptureJPEGPicture(userId, channel, ref para, pathBytes);
			return ok;
		}
		finally
		{
			NET_DVR.NET_DVR_Logout(userId);
		}
	}

	// Placeholder for real-time stream obtaining. In many apps this returns a handle or starts a callback.
	public bool TryOpenRealStream(HikvisionDeviceConfig config, int channel)
	{
		ArgumentNullException.ThrowIfNull(config);
		EnsureInitialized();
		// Not implemented here to keep scope minimal for unit test; can be expanded later.
		return IsOnline(config);
	}

	private static string BuildDeviceKey(HikvisionDeviceConfig config)
		=> $"{config.Ip}:{config.Port}:{config.Username}";

	private static void EnsureInitialized()
	{
		if (!NET_DVR._initialized)
		{
			if (!NET_DVR.NET_DVR_Init())
			{
				throw new InvalidOperationException("NET_DVR_Init failed.");
			}
			NET_DVR._initialized = true;
			AppDomain.CurrentDomain.ProcessExit += (_, __) => NET_DVR.NET_DVR_Cleanup();
		}
	}

	private static bool TryLogin(HikvisionDeviceConfig config, out int userId)
	{
		NET_DVR.NET_DVR_DEVICEINFO_V40 devInfo = new NET_DVR.NET_DVR_DEVICEINFO_V40();
		var loginInfo = new NET_DVR.NET_DVR_USER_LOGIN_INFO
		{
			sDeviceAddress = ToFixedBytes(config.Ip, 129),
			sUserName = ToFixedBytes(config.Username, 64),
			sPassword = ToFixedBytes(config.Password, 64),
			wPort = (ushort)config.Port,
			bUseAsynLogin = 0
		};
		userId = NET_DVR.NET_DVR_Login_V40(ref loginInfo, ref devInfo);
		return userId >= 0;
	}

	private static byte[] ToFixedBytes(string text, int fixedLen)
	{
		var bytes = Encoding.ASCII.GetBytes(text ?? string.Empty);
		Array.Resize(ref bytes, fixedLen);
		return bytes;
	}
}

public sealed class HikvisionDeviceConfig
{
	public string Ip { get; set; } = string.Empty;
	public string Username { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public int Port { get; set; }
	public int StreamType { get; set; }
	public int[] Channels { get; set; } = Array.Empty<int>();
}

internal static class NET_DVR
{
	internal static bool _initialized;

	[StructLayout(LayoutKind.Sequential)]
	internal struct NET_DVR_DEVICEINFO_V30
	{
		public int dwSize;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] sSerialNumber;
		public int byAlarmInPortNum;
		public int byAlarmOutPortNum;
		public int byDiskNum;
		public int byDVRType;
		public int byChanNum;
		public int byStartChan;
		public int byAudioChanNum;
		public int byIPChanNum;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct NET_DVR_DEVICEINFO_V40
	{
		public NET_DVR_DEVICEINFO_V30 struDeviceV30;
		public int bySupportLock;
		public int byRetryLoginTime;
		public int byPasswordLevel;
		public int byProxyType;
		public int dwSurplusLockTime;
		public int byCharEncodeType;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)] public byte[] byRes2;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct NET_DVR_USER_LOGIN_INFO
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 129)] public byte[] sDeviceAddress;
		public byte byUseTransport;
		public ushort wPort;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] sUserName;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] sPassword;
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

	[StructLayout(LayoutKind.Sequential)]
	internal struct NET_DVR_JPEGPARA
	{
		public ushort wPicSize;
		public ushort wPicQuality;
	}

	[DllImport("HCNetSDK.dll")]
	internal static extern bool NET_DVR_Init();

	[DllImport("HCNetSDK.dll")]
	internal static extern void NET_DVR_Cleanup();

	[DllImport("HCNetSDK.dll")]
	internal static extern int NET_DVR_Login_V40(ref NET_DVR_USER_LOGIN_INFO pLoginInfo, ref NET_DVR_DEVICEINFO_V40 lpDeviceInfo);

	[DllImport("HCNetSDK.dll")]
	internal static extern bool NET_DVR_Logout(int lUserID);

	[DllImport("HCNetSDK.dll")]
	internal static extern bool NET_DVR_CaptureJPEGPicture(int lUserID, int lChannel, ref NET_DVR_JPEGPARA lpJpegPara, byte[] sPicFileName);

	[DllImport("HCNetSDK.dll")]
	internal static extern uint NET_DVR_GetLastError();
}


