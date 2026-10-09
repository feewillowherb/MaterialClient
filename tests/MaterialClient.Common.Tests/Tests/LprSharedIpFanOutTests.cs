using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Events;
using MaterialClient.Common.Services;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class LprSharedIpFanOutTests
{
    private static LicensePlateRecognitionConfig Row(
        string name,
        string ip,
        LprSiteType siteType,
        bool enableGateIo = false) =>
        new()
        {
            Name = name,
            Ip = ip,
            SiteType = siteType,
            EnableGateIo = enableGateIo,
            DeviceType = LprDeviceType.Vzvision
        };

    [Fact]
    public void FindAllByIp_ReturnsScaleAndFinishedProduct_DistinctNames()
    {
        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("scale-cam", "192.168.1.10", LprSiteType.Scale),
            Row("product-cam", "192.168.1.10", LprSiteType.FinishedProduct),
            Row("other", "192.168.1.99", LprSiteType.Checkpoint)
        };

        var matched = LicensePlateRecognitionConfig.FindAllByIp(configs, "192.168.1.10");

        Assert.Equal(2, matched.Count);
        Assert.Contains(matched, c => c.Name == "scale-cam");
        Assert.Contains(matched, c => c.Name == "product-cam");
    }

    [Fact]
    public void FindAllByIp_CollapsesDuplicateNames()
    {
        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("same", "10.0.0.1", LprSiteType.Scale),
            Row("same", "10.0.0.1", LprSiteType.FinishedProduct)
        };

        var matched = LicensePlateRecognitionConfig.FindAllByIp(configs, "10.0.0.1");

        Assert.Single(matched);
        Assert.Equal(LprSiteType.Scale, matched[0].SiteType);
    }

    [Fact]
    public void FindAllByIp_TrimsAndIgnoresCase()
    {
        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("a", " 10.0.0.5 ", LprSiteType.Scale)
        };

        var matched = LicensePlateRecognitionConfig.FindAllByIp(configs, "10.0.0.5");

        Assert.Single(matched);
    }

    [Fact]
    public async Task PublishFanOut_TwoEvents_WhenScaleAndProductShareIp()
    {
        var bus = new TestLocalEventBus();
        var received = new List<LicensePlateRecognizedEventData>();
        bus.Subscribe<LicensePlateRecognizedEventData>(e =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("scale-cam", "192.168.1.10", LprSiteType.Scale),
            Row("product-cam", "192.168.1.10", LprSiteType.FinishedProduct)
        };

        var payload = new LprRecognitionPayload(
            "浙A12345",
            null,
            null,
            null,
            null,
            LprDeviceType.Vzvision,
            DateTime.Now,
            "Lpr/x.jpg");

        await LprRecognitionEventPublisher.PublishFanOutAsync(
            bus, configs, "192.168.1.10", configs[0], payload);

        Assert.Equal(2, received.Count);
        Assert.Contains(received, e => e.DeviceName == "scale-cam");
        Assert.Contains(received, e => e.DeviceName == "product-cam");
        Assert.All(received, e =>
        {
            Assert.Equal("浙A12345", e.PlateNumber);
            Assert.Equal("Lpr/x.jpg", e.LprImagePath);
            Assert.Equal("192.168.1.10", e.DeviceIp);
        });
    }

    [Fact]
    public async Task PublishFanOut_SingleEvent_WhenOneRow()
    {
        var bus = new TestLocalEventBus();
        var received = new List<LicensePlateRecognizedEventData>();
        bus.Subscribe<LicensePlateRecognizedEventData>(e =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("only", "192.168.1.10", LprSiteType.Scale)
        };

        await LprRecognitionEventPublisher.PublishFanOutAsync(
            bus,
            configs,
            "192.168.1.10",
            configs[0],
            new LprRecognitionPayload("浙A1", null, null, null, null, LprDeviceType.Hikvision, DateTime.Now, null));

        Assert.Single(received);
        Assert.Equal("only", received[0].DeviceName);
    }

    [Fact]
    public async Task PublishFanOut_Fallback_WhenSettingsEmpty()
    {
        var bus = new TestLocalEventBus();
        var received = new List<LicensePlateRecognizedEventData>();
        bus.Subscribe<LicensePlateRecognizedEventData>(e =>
        {
            received.Add(e);
            return Task.CompletedTask;
        });

        var fallback = Row("sdk-winner", "192.168.1.10", LprSiteType.FinishedProduct);

        await LprRecognitionEventPublisher.PublishFanOutAsync(
            bus,
            [],
            "192.168.1.10",
            fallback,
            new LprRecognitionPayload("浙A1", null, null, null, null, LprDeviceType.Vzvision, DateTime.Now, null));

        Assert.Single(received);
        Assert.Equal("sdk-winner", received[0].DeviceName);
    }

    [Fact]
    public void SelectFirstValidPerIp_DedupesSharedIp()
    {
        var configs = new List<LicensePlateRecognitionConfig>
        {
            Row("scale-cam", "192.168.1.10", LprSiteType.Scale),
            Row("product-cam", "192.168.1.10", LprSiteType.FinishedProduct),
            Row("other", "192.168.1.11", LprSiteType.Checkpoint)
        };

        var selected = LicensePlateRecognitionConfig.SelectFirstValidPerIp(configs);

        Assert.Equal(2, selected.Count);
        Assert.Equal("scale-cam", selected[0].Name);
        Assert.Equal("other", selected[1].Name);
    }

    [Fact]
    public void HasOtherScaleGateSibling_True_ForProductWhenScaleAlsoEnablesGate()
    {
        var scale = Row("scale-cam", "192.168.1.10", LprSiteType.Scale, enableGateIo: true);
        var product = Row("product-cam", "192.168.1.10", LprSiteType.FinishedProduct, enableGateIo: true);
        var configs = new[] { scale, product };

        Assert.True(LicensePlateRecognitionConfig.HasOtherScaleGateSibling(configs, product, "192.168.1.10"));
        Assert.False(LicensePlateRecognitionConfig.HasOtherScaleGateSibling(configs, scale, "192.168.1.10"));
    }

    [Fact]
    public void HasOtherScaleGateSibling_False_WhenOnlyProductEnablesGate()
    {
        var scale = Row("scale-cam", "192.168.1.10", LprSiteType.Scale, enableGateIo: false);
        var product = Row("product-cam", "192.168.1.10", LprSiteType.FinishedProduct, enableGateIo: true);

        Assert.False(LicensePlateRecognitionConfig.HasOtherScaleGateSibling([scale, product], product, "192.168.1.10"));
    }
}
