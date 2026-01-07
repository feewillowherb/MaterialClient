using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for WeighingServiceState
/// </summary>
public class WeighingServiceStateTests
{
    [Fact]
    public void Initial_Should_HaveCorrectDefaultValues()
    {
        // Act
        var initialState = WeighingServiceState.Initial;

        // Assert
        initialState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        initialState.Weight.ShouldBe(0m);
        initialState.Stability.IsStable.ShouldBeFalse();
        initialState.Stability.StableWeight.ShouldBeNull();
        initialState.DeliveryType.ShouldBe(DeliveryType.Receiving);
        initialState.LastCreatedWeighingRecordId.ShouldBeNull();
        initialState.PlateNumberCache.ShouldNotBeNull();
        initialState.PlateNumberCache.IsEmpty.ShouldBeTrue();
        initialState.Config.ShouldNotBeNull();
    }

    [Fact]
    public void ActionTypes_Should_BeInstantiable()
    {
        // Act & Assert - Verify all action types can be created
        var weightAction = new WeightUpdatedAction(1.5m);
        weightAction.Weight.ShouldBe(1.5m);

        var stabilityInfo = new WeightStabilityInfo
        {
            Weight = 1.0m,
            IsStable = true,
            StableWeight = 1.0m,
            Min = 0.95m,
            Max = 1.05m,
            Range = 0.1m
        };
        var stabilityAction = new StabilityUpdatedAction(stabilityInfo);
        stabilityAction.Stability.ShouldBe(stabilityInfo);

        var deliveryTypeAction = new SetDeliveryTypeAction(DeliveryType.Sending);
        deliveryTypeAction.DeliveryType.ShouldBe(DeliveryType.Sending);

        var plateAction = new PlateNumberRecognizedAction("京A12345");
        plateAction.PlateNumber.ShouldBe("京A12345");

        var recordAction = new WeighingRecordCreatedAction(123L);
        recordAction.RecordId.ShouldBe(123L);

        var resetAction = new ResetWeighingCycleAction();
        resetAction.ShouldNotBeNull();

        var config = new WeighingConfiguration();
        var configAction = new ConfigurationUpdatedAction(config);
        configAction.Config.ShouldBe(config);
    }

    [Fact]
    public void State_Should_BeImmutable_WithWithExpression()
    {
        // Arrange
        var initialState = WeighingServiceState.Initial;

        // Act
        var newState = initialState with
        {
            Status = AttendedWeighingStatus.WaitingForStability,
            Weight = 1.5m,
            DeliveryType = DeliveryType.Sending
        };

        // Assert
        initialState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        initialState.Weight.ShouldBe(0m);
        initialState.DeliveryType.ShouldBe(DeliveryType.Receiving);

        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForStability);
        newState.Weight.ShouldBe(1.5m);
        newState.DeliveryType.ShouldBe(DeliveryType.Sending);
    }
}

