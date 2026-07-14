using MaterialClient.Common.Services.AttendedWeighing;
using MaterialClient.Common.Services.AttendedWeighing.Records;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class CycleLprCandidatePriorityTests
{
    private static WeighingStateManager CreateManager()
    {
        var eventBus = Substitute.For<Volo.Abp.EventBus.Local.ILocalEventBus>();
        var logger = Substitute.For<ILogger<WeighingStateManager>>();
        return new WeighingStateManager(eventBus, logger);
    }

    [Fact]
    public void TryAccept_Should_AcceptFirstCandidate()
    {
        var manager = CreateManager();
        var accepted = manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/a.jpg", false, DateTime.UtcNow));

        accepted.ShouldBeTrue();
        manager.GetCurrentCycleLprImagePath().ShouldBe("Lpr/a.jpg");
    }

    [Fact]
    public void TryAccept_Should_UpgradePlateLessWithPlated()
    {
        var manager = CreateManager();
        var t0 = DateTime.UtcNow;
        manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/none.jpg", false, t0));

        var accepted = manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/plate.jpg", true, t0.AddSeconds(1)));

        accepted.ShouldBeTrue();
        manager.GetCurrentCycleLprCandidate()!.RelativePath.ShouldBe("Lpr/plate.jpg");
        manager.GetCurrentCycleLprCandidate()!.HasPlate.ShouldBeTrue();
    }

    [Fact]
    public void TryAccept_Should_RejectPlateLessAfterPlated()
    {
        var manager = CreateManager();
        var t0 = DateTime.UtcNow;
        manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/plate.jpg", true, t0));

        var accepted = manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/none.jpg", false, t0.AddSeconds(5)));

        accepted.ShouldBeFalse();
        manager.GetCurrentCycleLprImagePath().ShouldBe("Lpr/plate.jpg");
    }

    [Fact]
    public void TryAccept_Should_ReplaceSamePriorityWithNewer()
    {
        var manager = CreateManager();
        var t0 = DateTime.UtcNow;
        manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/old.jpg", true, t0));

        var accepted = manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/new.jpg", true, t0.AddSeconds(2)));

        accepted.ShouldBeTrue();
        manager.GetCurrentCycleLprImagePath().ShouldBe("Lpr/new.jpg");
    }

    [Fact]
    public void ResetCycle_Should_ClearCandidateAndRecordId()
    {
        var manager = CreateManager();
        manager.TryAcceptLprCandidate(new CycleLprCandidate("Lpr/a.jpg", true, DateTime.UtcNow));
        manager.SetLastCreatedWeighingRecordId(42);

        manager.ResetCycle();

        manager.GetCurrentCycleLprCandidate().ShouldBeNull();
        manager.GetLastCreatedWeighingRecordId().ShouldBeNull();
    }

    [Fact]
    public void TriggerLprCaptureDelayMs_Default_Should_BeZero()
    {
        var settings = new MaterialClient.Common.Configuration.SystemSettings();
        settings.TriggerLprCaptureDelayMs.ShouldBe(0);
        settings.EnableTriggerLprCapture.ShouldBeFalse();
    }
}
