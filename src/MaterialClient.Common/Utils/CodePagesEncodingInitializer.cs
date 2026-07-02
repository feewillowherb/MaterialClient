using System.Text;

namespace MaterialClient.Common.Utils;

/// <summary>
///     注册 CodePages 编码提供程序，使 GBK/GB2312 等代码页在 .NET 运行时可用。
///     .NET 10 已由框架提供 <c>System.Text.Encoding.CodePages</c>，无需额外 NuGet 包；仅需调用 <see cref="Register"/>。
/// </summary>
public static class CodePagesEncodingInitializer
{
    private static int _registered;

    /// <summary>
    ///     注册 <see cref="CodePagesEncodingProvider"/>（幂等，可多次调用）。
    ///     应在应用启动最早阶段调用。
    /// </summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
