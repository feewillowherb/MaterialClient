using MaterialClient.EFCore;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace MaterialClient.Common.Tests.Tests;

public class KernelModelExcludesProductEntitiesTests
{
    [Fact]
    public void Kernel_model_does_not_include_Urban_or_Recycle_entity_types()
    {
        var options = new DbContextOptionsBuilder<MaterialClientDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new MaterialClientDbContext(options);
        var names = context.Model.GetEntityTypes().Select(t => t.ClrType.Name).ToList();

        names.ShouldNotContain("UrbanWeighingExtension");
        names.ShouldNotContain("RecycleWaybillExtension");
        names.ShouldNotContain("UrbanSettingsRow");
    }
}
