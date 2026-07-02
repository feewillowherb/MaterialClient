using System.Text;

namespace MaterialClient.Common.Utils;

/// <summary>
///     注册 CodePages 编码提供程序，使 GBK/GB2312 等代码页在 .NET Core 运行时可用。
/// </summary>
public static class CodePagesEncodingInitializer
{
    private static int _registered;

    /// <summary>
    ///     注册 <see cref="CodePagesEncodingProvider"/>（幂等，可多次调用）。
    ///     应在应用启动最早阶段调用，且需引用 System.Text.Encoding.CodePages 包。
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
