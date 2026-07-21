using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Models;
using MaterialClient.Common.Services;
using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.Authentication;
using MaterialClient.Common.Services.Hardware;
using MaterialClient.Common.Services.Hikvision;
using MaterialClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace MaterialClient.UI.Tests.Tests;

/// <summary>
///     8.7 UI 层行为测试（VM 级）：
///     <list type="number">
///         <item>Recycle 模式已完成运单显示「收货」（<see cref="AttendedWeighingViewModel.CanReceive" />），
///         SolidWaste 模式显示「打印」（<see cref="AttendedWeighingViewModel.CanPrintSolidWaste" />）——两者互斥。</item>
///         <item>收货对话框必填校验：收货时间、收货照片缺失时给出提示且不产出结果；齐全时构造结果。</item>
///     </list>
///     「表单回填」（RecycleWeighingDetailViewModel 按 WaybillId 回填 RecycleWaybillExtension 的
///     UnitPrice/SaleContractNo）经 Avalonia Dispatcher.UIThread.Post 延迟加载，无 Avalonia 宿主无法可靠触发；
///     其数据访问契约（按 WaybillId 查询 RecycleWaybillExtension）已由 RecycleWeighingServiceUpsertTests（8.5）
///     的 EF 宿主端到端覆盖（同一查询模式 + 读回断言），此处不重复。
/// </summary>
public class RecycleReceivingUiTests
{
    // ===== 1. 收货/打印按钮互斥（Recycle 显示收货，SolidWaste 显示打印） =====

    [Fact]
    public async Task Recycle_Completed_Waybill_Shows_Receive_Not_Print()
    {
        var vm = CreateViewModel();
        await Task.Delay(300); // 允许构造函数 fire-and-forget 初始化完成

        vm.SelectedListItem = new WeighingListItemDto
        {
            Id = 7001,
            ItemType = WeighingListItemType.Waybill,
            OrderType = OrderTypeEnum.Completed,
            WeighingMode = WeighingMode.Recycle
        };

        Assert.True(vm.CanReceive);
        Assert.False(vm.CanPrintSolidWaste);
    }

    [Fact]
    public async Task SolidWaste_Completed_Waybill_Shows_Print_Not_Receive()
    {
        var vm = CreateViewModel();
        await Task.Delay(300);

        vm.SelectedListItem = new WeighingListItemDto
        {
            Id = 7002,
            ItemType = WeighingListItemType.Waybill,
            OrderType = OrderTypeEnum.Completed,
            WeighingMode = WeighingMode.SolidWaste
        };

        Assert.False(vm.CanReceive);
        Assert.True(vm.CanPrintSolidWaste);
    }

    // ===== 2. 收货对话框必填校验 =====

    [Fact]
    public async Task Confirm_Without_Image_Sets_Error_And_No_Result()
    {
        var vm = new RecycleReceivingViewModel();
        vm.Initialize("fl-7001");
        vm.ReceivingDate = new DateTime(2026, 7, 9);
        // 收货照片为空

        await vm.ConfirmCommand.Execute().ToTask();

        Assert.Null(vm.Result);
        Assert.Equal("收货照片为必填", vm.ErrorMessage);
    }

    [Fact]
    public async Task Confirm_Without_Date_Sets_Error_And_No_Result()
    {
        var vm = new RecycleReceivingViewModel();
        vm.Initialize("fl-7001");
        vm.ReceivingDate = null;
        vm.SelectedImagePath = @"C:\fake-receiving.jpg";

        await vm.ConfirmCommand.Execute().ToTask();

        Assert.Null(vm.Result);
        Assert.Equal("收货时间为必填", vm.ErrorMessage);
    }

    [Fact]
    public async Task Confirm_With_Date_And_Image_Builds_Result()
    {
        var vm = new RecycleReceivingViewModel();
        vm.Initialize("fl-7001");
        vm.ReceivingDate = new DateTime(2026, 7, 9);
        vm.ReceivingTimeOfDay = new TimeSpan(15, 30, 0);
        vm.SelectedImagePath = @"C:\fake-receiving.jpg";

        await vm.ConfirmCommand.Execute().ToTask();

        Assert.NotNull(vm.Result);
        Assert.Equal(new DateTime(2026, 7, 9, 15, 30, 0), vm.Result!.ReceivingTime);
        Assert.Equal(@"C:\fake-receiving.jpg", vm.Result!.ImagePath);
    }

    private static AttendedWeighingViewModel CreateViewModel()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.GetSettingsAsync().Returns(DefaultSettings());
        settingsService.GetWeighingModeAsync().Returns(WeighingMode.Recycle);

        var services = new ServiceCollection();
        services.AddSingleton(_ => Substitute.For<ILogger<AttendedWeighingViewModel>>());
        var serviceProvider = services.BuildServiceProvider();

        return new AttendedWeighingViewModel(
            Substitute.For<IWeighingMatchingService>(),
            serviceProvider,
            Substitute.For<ITruckScaleWeightService>(),
            Substitute.For<IAttendedWeighingService>(),
            Substitute.For<IAuthenticationService>(),
            Substitute.For<ISoundDeviceService>(),
            settingsService,
            Substitute.For<ILprDeviceOnlineStatusService>(),
            Substitute.For<ISyncMaterialService>(),
            Substitute.For<IAttachmentService>(),
            Substitute.For<IRecycleReceivingService>(),
            new TestLocalEventBus());
    }

    private static SettingsEntity DefaultSettings()
    {
        return new SettingsEntity(
            new ScaleSettings(),
            new DocumentScannerConfig(),
            new SystemSettings { DefaultWeighingMode = WeighingMode.Recycle },
            [],
            [],
            new WeighingConfiguration(),
            new SoundDeviceSettings());
    }
}
