using MaterialClient.Common.Services.Vzvision;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Vzvision (Vz) SDK 集成测试：仅验证登录与连通性（不接入业务逻辑）。
/// 默认 Skip，需在真实设备环境下启用。
/// </summary>
[Collection("Vzvision")]
public class VzvisionIntegrationTests(ITestOutputHelper output)
{
    /// <summary>
    /// 测试配置：IP / 账号 / 密码由需求给定。
    /// 端口使用 SDK Demo 的默认值 80（如实际设备端口不同，请调整）。
    /// </summary>
    private const string TestIp = "192.168.3.191";
    private const string TestUsername = "admin";
    private const string TestPassword = "admin";
    private const ushort TestPort = 80;

    /// <summary>
    /// 验证：SDK Setup -> Open -> IsConnected -> Close 过程是否可正常工作。
    /// </summary>
    [Fact(Skip = "Requires physical Vzvision device (VzLPRSDK.dll + device connectivity)")]
    public void VzLogin_ShouldConnect()
    {
        int handle = 0;
        try
        {
            var setupRet = VzvisionSdk.VzLPRClient_Setup();
            Assert.True(setupRet == 0, $"VzLPRClient_Setup failed: {setupRet}");

            handle = VzvisionSdk.VzLPRClient_Open(TestIp, TestPort, TestUsername, TestPassword);
            Assert.True(handle != 0, "VzLPRClient_Open returned 0 handle (login/open failed)");

            var connRet = VzvisionSdk.VzLPRClient_IsConnected(handle, out var status);
            Assert.True(connRet == 0, $"VzLPRClient_IsConnected failed: {connRet}");
            Assert.True(status == 1, $"Device should be connected, status={status} (expected 1)");

            output.WriteLine(
                $"Vz login ok: ip={TestIp}, port={TestPort}, handle={handle}, status={status}");
        }
        finally
        {
            try
            {
                if (handle != 0)
                {
                    _ = VzvisionSdk.VzLPRClient_Close(handle);
                }
            }
            finally
            {
                VzvisionSdk.VzLPRClient_Cleanup();
            }
        }
    }

    /// <summary>
    /// 测试 GPIO/IO 输出通道设置能力（调用 SetIOOutput / GetIOOutput）。
    /// 注意：具体通道号与接线需要以设备实际配置为准。
    /// </summary>
    [Fact(Skip = "Requires physical Vzvision device (VzLPRSDK.dll + device connectivity)")]
    public void VzGpioSet_ShouldSetOutput()
    {
        int handle = 0;
        const uint ioChannel = 0; // GPIO/IO 输出通道0（如实际设备通道不同请改）

        try
        {
            var setupRet = VzvisionSdk.VzLPRClient_Setup();
            Assert.True(setupRet == 0, $"VzLPRClient_Setup failed: {setupRet}");

            handle = VzvisionSdk.VzLPRClient_Open(TestIp, TestPort, TestUsername, TestPassword);
            Assert.True(handle != 0, "VzLPRClient_Open returned 0 handle (login/open failed)");

            var setRet = VzvisionSdk.VzLPRClient_SetIOOutput(handle, ioChannel, 1);
            Assert.True(setRet == 0, $"VzLPRClient_SetIOOutput failed: {setRet}");

            // 给设备一个回写/状态刷新时间
            Thread.Sleep(100);

            var getRet = VzvisionSdk.VzLPRClient_GetIOOutput(handle, ioChannel, out var outputState);
            Assert.True(getRet == 0, $"VzLPRClient_GetIOOutput failed: {getRet}");
            Assert.True(outputState == 1, $"IO output state should be 1 after set, but got {outputState}");

            output.WriteLine($"Vz IO set ok: ip={TestIp}, channel={ioChannel}, state={outputState}");
        }
        finally
        {
            try
            {
                if (handle != 0)
                {
                    // 尽量在退出前复位输出，避免影响现场设备
                    _ = VzvisionSdk.VzLPRClient_SetIOOutput(handle, 0, 0);
                    _ = VzvisionSdk.VzLPRClient_Close(handle);
                }
            }
            finally
            {
                VzvisionSdk.VzLPRClient_Cleanup();
            }
        }
    }
}

/// <summary>
/// 禁止并行，避免 SDK 全局初始化/释放与其他硬件测试资源争用。
/// </summary>
[CollectionDefinition("Vzvision", DisableParallelization = true)]
public class VzvisionTestCollection
{
}

