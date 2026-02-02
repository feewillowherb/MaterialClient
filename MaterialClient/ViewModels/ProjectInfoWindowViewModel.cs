using System;
using System.Threading.Tasks;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Extensions;
using MaterialClient.Common.Services.Authentication;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Volo.Abp.DependencyInjection;

namespace MaterialClient.ViewModels;

/// <summary>
///     项目信息窗口 ViewModel
/// </summary>
public partial class ProjectInfoWindowViewModel : ReactiveViewModelBase, ITransientDependency
{
    private const int ProductNameTapThreshold = 20;

    private readonly IAuthenticationService _authenticationService;
    private readonly ILicenseService _licenseService;

    private int _productNameTapCount;

    [Reactive] private string _projectName = string.Empty;

    [Reactive] private string _productNameDisplay = string.Empty;

    [Reactive] private string _expirationDate = string.Empty;

    [Reactive] private string _machineCode = string.Empty;

    [Reactive] private string _authCode = string.Empty;

    public ProjectInfoWindowViewModel(
        IAuthenticationService authenticationService,
        ILicenseService licenseService)
    {
        _authenticationService = authenticationService;
        _licenseService = licenseService;
        ProductNameTapCommand = ReactiveCommand.CreateFromTask(OnProductNameDisplayTappedAsync);
    }

    /// <summary>
    ///     点击产品名称时触发；连续点击 20 次后删除 LicenseInfo、UserSession、UserCredential 并刷新
    /// </summary>
    public IReactiveCommand ProductNameTapCommand { get; }

    /// <summary>
    ///     初始化数据
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // 获取用户会话信息
            var session = await _authenticationService.GetCurrentSessionAsync();
            if (session != null)
            {
                ProjectName = session.CompanyName ?? string.Empty;
                ProductNameDisplay = TryGetProductCodeDescription(session.ProductId);
            }
            else
            {
                ProjectName = "未登录";
                ProductNameDisplay = "获取失败";
            }

            // 获取授权信息
            var license = await _licenseService.GetCurrentLicenseAsync();
            if (license != null)
            {
                // 格式化到期时间
                ExpirationDate = license.AuthEndTime.ToString("yyyy-MM-dd");

                // 处理机器码（部分隐藏）
                if (!string.IsNullOrEmpty(license.MachineCode))
                {
                    MachineCode = MaskCode(license.MachineCode);
                }
                else
                {
                    MachineCode = string.Empty;
                }

                // 处理授权码（Guid转字符串并部分隐藏）
                if (license.AuthToken.HasValue)
                {
                    var authTokenString = license.AuthToken.Value.ToString("N"); // 无连字符格式
                    AuthCode = MaskCode(authTokenString);
                }
                else
                {
                    AuthCode = string.Empty;
                }
            }
            else
            {
                ExpirationDate = "未授权";
                MachineCode = string.Empty;
                AuthCode = string.Empty;
            }
        }
        catch (Exception)
        {
            // 如果获取数据失败，显示默认值
            ProjectName = "获取失败";
            ProductNameDisplay = "获取失败";
            ExpirationDate = "获取失败";
            MachineCode = string.Empty;
            AuthCode = string.Empty;
        }
    }

    /// <summary>
    ///     将 UserSession.ProductId 转为 ProductCode 的 Description；转换失败返回「获取失败」。
    /// </summary>
    private static string TryGetProductCodeDescription(long productId)
    {
        try
        {
            var value = (int)productId;
            if (Enum.IsDefined(typeof(ProductCode), value))
                return ((ProductCode)value).GetDescription();
        }
        catch (OverflowException)
        {
            // productId 超出 int 范围
        }

        return "获取失败";
    }

    private async Task OnProductNameDisplayTappedAsync()
    {
        _productNameTapCount++;
        if (_productNameTapCount < ProductNameTapThreshold)
            return;

        _productNameTapCount = 0;
        await _authenticationService.ClearAllAuthDataAsync();
        await InitializeAsync();
    }

    /// <summary>
    ///     隐藏代码中间部分（显示前4位和后4位）
    /// </summary>
    private static string MaskCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length <= 8)
            return code;

        return code.Substring(0, 4) + "****" + code.Substring(code.Length - 4);
    }
}
