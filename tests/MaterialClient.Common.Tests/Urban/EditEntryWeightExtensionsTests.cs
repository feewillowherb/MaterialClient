using MaterialClient.Common.Entities.Enums;
using MaterialClient.Common.Entities.Urban;
using MaterialClient.Common.Utils;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Urban;

public class EditEntryWeightExtensionsTests
{
    [Fact]
    public void FromClientWeighing_converts_ton_to_kg_in_snapshot()
    {
        var snapshot = EditEntrySnapshotExtensions.FromClientWeighing("浙A12345", 8.5m, "test");

        snapshot.PlateNumber.ShouldBe("浙A12345");
        snapshot.TotalWeight.ShouldBe(MaterialMath.ConvertTonToKg(8.5m));
        snapshot.AnomalyReason.ShouldBe("test");
    }

    [Fact]
    public void NormalizeWeightsForServer_leaves_server_entry_unchanged()
    {
        var entry = new EditEntry
        {
            Source = EditSource.Server,
            Before = new EditEntrySnapshot { TotalWeight = 25000m },
            After = new EditEntrySnapshot { TotalWeight = 26000m }
        };

        var normalized = entry.NormalizeWeightsForServer();

        normalized.Before.TotalWeight.ShouldBe(25000m);
        normalized.After.TotalWeight.ShouldBe(26000m);
    }

    [Fact]
    public void NormalizeWeightsForServer_converts_legacy_client_ton_values()
    {
        var entry = new EditEntry
        {
            Source = EditSource.Client,
            Before = new EditEntrySnapshot { TotalWeight = 8.5m },
            After = new EditEntrySnapshot { TotalWeight = 9m }
        };

        var normalized = entry.NormalizeWeightsForServer();

        normalized.Before.TotalWeight.ShouldBe(8500m);
        normalized.After.TotalWeight.ShouldBe(9000m);
    }

    [Fact]
    public void NormalizeWeightsForServer_leaves_client_kg_values_unchanged()
    {
        var entry = new EditEntry
        {
            Source = EditSource.Client,
            Before = new EditEntrySnapshot { TotalWeight = 8500m },
            After = new EditEntrySnapshot { TotalWeight = 9000m }
        };

        var normalized = entry.NormalizeWeightsForServer();

        normalized.Before.TotalWeight.ShouldBe(8500m);
        normalized.After.TotalWeight.ShouldBe(9000m);
    }
}
