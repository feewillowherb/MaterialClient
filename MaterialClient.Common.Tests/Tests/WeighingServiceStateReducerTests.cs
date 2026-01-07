using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Services;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for WeighingServiceStateReducer
/// </summary>
public class WeighingServiceStateReducerTests
{
    private readonly WeighingConfiguration _defaultConfig = new()
    {
        MinWeightThreshold = 0.5m,
        WeightStabilityThreshold = 0.05m,
        StabilityWindowMs = 3000,
        StabilityCheckIntervalMs = 200
    };

    #region WeightUpdate Reductions

    [Fact]
    public void ReduceWeightUpdate_OffScale_To_WaitingForStability_WhenWeightAboveThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with { Config = _defaultConfig };
        var action = new WeightUpdatedAction(1.0m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForStability);
        newState.Weight.ShouldBe(1.0m);
    }

    [Fact]
    public void ReduceWeightUpdate_OffScale_To_OffScale_WhenWeightBelowThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with { Config = _defaultConfig };
        var action = new WeightUpdatedAction(0.3m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        newState.Weight.ShouldBe(0.3m);
    }

    [Fact]
    public void ReduceWeightUpdate_WaitingForStability_To_OffScale_WhenWeightBelowThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            Weight = 1.0m
        };
        var action = new WeightUpdatedAction(0.3m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        newState.Weight.ShouldBe(0.3m);
    }

    [Fact]
    public void ReduceWeightUpdate_WaitingForStability_To_WeightStabilized_WhenStableAndNotWeighed()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            Weight = 1.0m,
            Stability = new WeightStabilityInfo
            {
                IsStable = true,
                StableWeight = 1.0m,
                Min = 0.95m,
                Max = 1.05m,
                Range = 0.1m
            },
            LastCreatedWeighingRecordId = null
        };
        var action = new WeightUpdatedAction(1.0m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WeightStabilized);
        newState.Weight.ShouldBe(1.0m);
    }

    [Fact]
    public void ReduceWeightUpdate_WaitingForStability_To_WaitingForStability_WhenStableButAlreadyWeighed()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            Weight = 1.0m,
            Stability = new WeightStabilityInfo { IsStable = true },
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeightUpdatedAction(1.0m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForDeparture);
    }

    [Fact]
    public void ReduceWeightUpdate_WeightStabilized_To_WaitingForDeparture_WhenRecordCreated()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WeightStabilized,
            Weight = 1.0m,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeightUpdatedAction(1.0m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForDeparture);
    }

    [Fact]
    public void ReduceWeightUpdate_WeightStabilized_To_OffScale_WhenWeightBelowThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WeightStabilized,
            Weight = 1.0m
        };
        var action = new WeightUpdatedAction(0.3m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        newState.LastCreatedWeighingRecordId.ShouldBeNull();
    }

    [Fact]
    public void ReduceWeightUpdate_WaitingForDeparture_To_OffScale_WhenWeightBelowThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForDeparture,
            Weight = 1.0m,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeightUpdatedAction(0.3m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        newState.LastCreatedWeighingRecordId.ShouldBeNull();
    }

    [Fact]
    public void ReduceWeightUpdate_Should_ClearRecordId_OnOffScaleTransition()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForDeparture,
            Weight = 1.0m,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeightUpdatedAction(0.3m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.OffScale);
        newState.LastCreatedWeighingRecordId.ShouldBeNull();
    }

    [Fact]
    public void ReduceWeightUpdate_Should_ForceWaitingForDeparture_WhenRecordExistsAndWeightAboveThreshold()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WeightStabilized,
            Weight = 1.0m,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeightUpdatedAction(1.0m);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForDeparture);
    }

    #endregion

    #region StabilityUpdate Reductions

    [Fact]
    public void ReduceStabilityUpdate_WaitingForStability_To_WeightStabilized_WhenStable()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            LastCreatedWeighingRecordId = null
        };
        var stabilityInfo = new WeightStabilityInfo
        {
            IsStable = true,
            StableWeight = 1.0m,
            Min = 0.95m,
            Max = 1.05m,
            Range = 0.1m
        };
        var action = new StabilityUpdatedAction(stabilityInfo);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WeightStabilized);
        newState.Stability.ShouldBe(stabilityInfo);
    }

    [Fact]
    public void ReduceStabilityUpdate_WaitingForStability_To_WaitingForStability_WhenAlreadyWeighed()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            LastCreatedWeighingRecordId = 123L
        };
        var stabilityInfo = new WeightStabilityInfo { IsStable = true };
        var action = new StabilityUpdatedAction(stabilityInfo);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Status.ShouldBe(AttendedWeighingStatus.WaitingForStability);
    }

    [Fact]
    public void ReduceStabilityUpdate_Should_UpdateStabilityInfo()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.OffScale
        };
        var stabilityInfo = new WeightStabilityInfo
        {
            IsStable = false,
            StableWeight = null,
            Min = 0.9m,
            Max = 1.1m,
            Range = 0.2m
        };
        var action = new StabilityUpdatedAction(stabilityInfo);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Stability.ShouldBe(stabilityInfo);
    }

    #endregion

    #region PlateNumberRecognized Reductions

    [Fact]
    public void ReducePlateNumberRecognized_Should_CachePlateNumber_WhenOnScale()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability
        };
        var action = new PlateNumberRecognizedAction("京A12345");

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache.ShouldNotBeNull();
        newState.PlateNumberCache.ContainsKey("京A12345").ShouldBeTrue();
        newState.PlateNumberCache["京A12345"].Count.ShouldBe(1);
    }

    [Fact]
    public void ReducePlateNumberRecognized_Should_Ignore_WhenOffScale()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.OffScale
        };
        var action = new PlateNumberRecognizedAction("京A12345");

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ReducePlateNumberRecognized_Should_UpdateCount_ForExistingPlateNumber()
    {
        // Arrange
        var cache = new ConcurrentDictionary<string, PlateNumberCacheRecord>();
        cache.TryAdd("京A12345", new PlateNumberCacheRecord { Count = 2, LastUpdateTime = DateTime.UtcNow });
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            PlateNumberCache = cache
        };
        var action = new PlateNumberRecognizedAction("京A12345");

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache["京A12345"].Count.ShouldBe(3);
    }

    [Fact]
    public void ReducePlateNumberRecognized_Should_Work_WhenWeightStabilized()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WeightStabilized
        };
        var action = new PlateNumberRecognizedAction("粤B67890");

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache.ContainsKey("粤B67890").ShouldBeTrue();
    }

    [Fact]
    public void ReducePlateNumberRecognized_Should_Work_WhenWaitingForDeparture()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForDeparture
        };
        var action = new PlateNumberRecognizedAction("沪C99999");

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache.ContainsKey("沪C99999").ShouldBeTrue();
    }

    #endregion

    #region ResetWeighingCycle Reductions

    [Fact]
    public void ReduceResetCycle_Should_ClearRecordId()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new ResetWeighingCycleAction();

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.LastCreatedWeighingRecordId.ShouldBeNull();
    }

    [Fact]
    public void ReduceResetCycle_Should_ClearPlateNumberCache()
    {
        // Arrange
        var cache = new ConcurrentDictionary<string, PlateNumberCacheRecord>();
        cache.TryAdd("京A12345", new PlateNumberCacheRecord { Count = 1, LastUpdateTime = DateTime.UtcNow });
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            PlateNumberCache = cache
        };
        var action = new ResetWeighingCycleAction();

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.PlateNumberCache.IsEmpty.ShouldBeTrue();
    }

    #endregion

    #region SetDeliveryType Reductions

    [Fact]
    public void ReduceSetDeliveryType_Should_UpdateDeliveryType()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            DeliveryType = DeliveryType.Receiving
        };
        var action = new SetDeliveryTypeAction(DeliveryType.Sending);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.DeliveryType.ShouldBe(DeliveryType.Sending);
    }

    #endregion

    #region WeighingRecordCreated Reductions

    [Fact]
    public void ReduceWeighingRecordCreated_Should_UpdateRecordId()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            LastCreatedWeighingRecordId = null
        };
        var action = new WeighingRecordCreatedAction(123L);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.LastCreatedWeighingRecordId.ShouldBe(123L);
    }

    [Fact]
    public void ReduceWeighingRecordCreated_Should_UpdateRecordId_ToNull()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            LastCreatedWeighingRecordId = 123L
        };
        var action = new WeighingRecordCreatedAction(null);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.LastCreatedWeighingRecordId.ShouldBeNull();
    }

    #endregion

    #region ConfigurationUpdated Reductions

    [Fact]
    public void ReduceConfigurationUpdated_Should_UpdateConfig()
    {
        // Arrange
        var state = WeighingServiceState.Initial;
        var newConfig = new WeighingConfiguration
        {
            MinWeightThreshold = 1.0m,
            WeightStabilityThreshold = 0.1m
        };
        var action = new ConfigurationUpdatedAction(newConfig);

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, action);

        // Assert
        newState.Config.ShouldBe(newConfig);
    }

    #endregion

    #region Unknown Action

    [Fact]
    public void ReduceState_Should_ReturnUnchangedState_ForUnknownAction()
    {
        // Arrange
        var state = WeighingServiceState.Initial with
        {
            Config = _defaultConfig,
            Status = AttendedWeighingStatus.WaitingForStability,
            Weight = 1.0m
        };
        var unknownAction = new UnknownAction();

        // Act
        var newState = WeighingServiceStateReducer.ReduceState(state, unknownAction);

        // Assert
        newState.ShouldBe(state);
    }

    private record UnknownAction : StateAction;

    #endregion
}

